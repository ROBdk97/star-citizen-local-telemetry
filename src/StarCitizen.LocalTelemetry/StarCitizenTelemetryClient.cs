using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Proto = Sc.Game.Telemetry.V1;

namespace StarCitizen.LocalTelemetry;

/// <summary>Client for Star Citizen's local telemetry gRPC service (default <c>http://127.0.0.1:2942</c>).</summary>
/// <remarks>
/// Thread-safe. Logs connection changes and schema mismatches to the optional <see cref="ILogger"/>.
/// To fake the game in tests, depend on <see cref="ITelemetryClient"/> instead.
/// </remarks>
public sealed partial class StarCitizenTelemetryClient : ITelemetryClient, IDisposable
{
    /// <summary>The game's default endpoint, <c>http://127.0.0.1:2942</c>.</summary>
    public static Uri DefaultAddress { get; } = new("http://127.0.0.1:2942");

    private readonly GrpcChannel? _channel;
    private readonly Proto.GameTelemetryService.GameTelemetryServiceClient _client;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly string _address;
    private readonly TimeSpan _retryDelay;
    private readonly bool _detectSchemaMismatches;
    private readonly ConcurrentDictionary<SchemaMismatch, byte> _seenMismatches = new();
    private readonly ConcurrentQueue<SchemaMismatch> _mismatches = new();

    /// <summary>Creates a client for <see cref="DefaultAddress"/> with default options. No connection is made until a method is called.</summary>
    public StarCitizenTelemetryClient()
        : this(address: null)
    {
    }

    /// <summary>Creates a client with default options. No connection is made until a method is called.</summary>
    /// <param name="address">The service address; <see cref="DefaultAddress"/> when <see langword="null"/>. The service is plaintext HTTP/2.</param>
    public StarCitizenTelemetryClient(Uri? address)
        : this(new StarCitizenTelemetryOptions { Address = address ?? DefaultAddress }, null)
    {
    }

    /// <summary>Creates a client. No connection is made until a method is called.</summary>
    /// <param name="options">The client settings.</param>
    /// <param name="logger">Receives connection and schema messages; <see langword="null"/> to disable logging.</param>
    /// <exception cref="ArgumentException"><paramref name="options"/> has no address or a non-positive retry delay.</exception>
    public StarCitizenTelemetryClient(StarCitizenTelemetryOptions options, ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Address is null)
        {
            throw new ArgumentException("The address is required.", nameof(options));
        }

        if (!IsValidRetryDelay(options.RetryDelay))
        {
            throw new ArgumentException("The retry delay must be positive.", nameof(options));
        }

        _channel = GrpcChannel.ForAddress(options.Address);
        _client = new Proto.GameTelemetryService.GameTelemetryServiceClient(_channel);
        _time = TimeProvider.System;
        _logger = logger ?? NullLogger.Instance;
        _address = options.Address.ToString();
        _retryDelay = options.RetryDelay;
        _detectSchemaMismatches = options.DetectSchemaMismatches;
    }

    internal StarCitizenTelemetryClient(
        Proto.GameTelemetryService.GameTelemetryServiceClient client,
        TimeProvider? time = null,
        ILogger? logger = null,
        StarCitizenTelemetryOptions? options = null)
    {
        options ??= new();
        _client = client;
        _time = time ?? TimeProvider.System;
        _logger = logger ?? NullLogger.Instance;
        _address = options.Address.ToString();
        _retryDelay = options.RetryDelay;
        _detectSchemaMismatches = options.DetectSchemaMismatches;
    }

    /// <summary>
    /// The distinct schema mismatches this client has seen so far, in the order they were first seen.
    /// Empty when the game matches the bundled schema or detection is disabled.
    /// </summary>
    public IReadOnlyCollection<SchemaMismatch> SchemaMismatches => _mismatches.ToArray();

    /// <inheritdoc/>
    /// <remarks>Each enumeration opens its own gRPC stream. Use <see cref="ObserveTelemetryAsync"/> for long-running readers.</remarks>
    public async IAsyncEnumerable<TelemetryFrame> ReadTelemetryAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var call = _client.TelemetryStream(new Proto.TelemetryStreamRequest(), cancellationToken: cancellationToken);

        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false);
            }
            catch (RpcException) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (!hasNext) yield break;
            var message = call.ResponseStream.Current;
            CheckSchema(message);
            yield return message.ToModel(_time.GetLocalNow());
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Each distinct <see cref="SchemaMismatch"/> is reported once per client, as a
    /// <see cref="TelemetryStreamEventKind.SchemaMismatch"/> event before the frame that contained it.
    /// </remarks>
    public async IAsyncEnumerable<TelemetryStreamEvent> ObserveTelemetryAsync(
        TimeSpan? retryDelay = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var delay = retryDelay ?? _retryDelay;
        if (!IsValidRetryDelay(delay))
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelay));
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Log.Connecting(_logger, _address);
            yield return new(TelemetryStreamEventKind.Connecting);
            cancellationToken.ThrowIfCancellationRequested();
            string? reason = null;
            var connected = false;
            using (var call = _client.TelemetryStream(
                       new Proto.TelemetryStreamRequest(), cancellationToken: cancellationToken))
            {
                while (true)
                {
                    bool hasNext;
                    try
                    {
                        hasNext = await call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false);
                    }
                    catch (RpcException ex) when (
                        ex.StatusCode == StatusCode.Unavailable && !cancellationToken.IsCancellationRequested)
                    {
                        reason = string.IsNullOrEmpty(ex.Status.Detail) ? "Unavailable" : $"Unavailable: {ex.Status.Detail}";
                        break;
                    }
                    catch (RpcException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    if (!hasNext)
                    {
                        reason = "Stream ended";
                        break;
                    }

                    if (!connected)
                    {
                        connected = true;
                        Log.Connected(_logger, _address);
                        yield return new(TelemetryStreamEventKind.Connected);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    var message = call.ResponseStream.Current;
                    if (CheckSchema(message) is { } mismatches)
                    {
                        foreach (var mismatch in mismatches)
                        {
                            yield return new(TelemetryStreamEventKind.SchemaMismatch, Mismatch: mismatch);
                        }
                    }

                    yield return new(TelemetryStreamEventKind.Frame, message.ToModel(_time.GetLocalNow()));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            Log.Disconnected(_logger, reason, delay);
            yield return new(TelemetryStreamEventKind.Disconnected, Reason: reason);
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GameAction>> GetActionsAsync(CancellationToken cancellationToken = default)
    {
        using var call = _client.ListTriggerableActionsAsync(
            new Proto.ListTriggerableActionsRequest(), cancellationToken: cancellationToken);
        var response = await call.ResponseAsync.ConfigureAwait(false);
        CheckSchema(response);
        return response.Actions.Select(action => action.ToModel()).ToArray();
    }

    /// <inheritdoc/>
    public async Task<ActionResult> TriggerActionAsync(
        string actionName,
        TimeSpan? hold = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName);

        var request = new Proto.TriggerGameActionRequest { ActionName = actionName };
        if (hold is { } duration)
        {
            if (duration < TimeSpan.Zero || duration.TotalMilliseconds > uint.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(hold));
            }

            request.HoldMs = (uint)duration.TotalMilliseconds;
        }

        Log.TriggeringAction(_logger, actionName, hold);
        using var call = _client.TriggerGameActionAsync(request, cancellationToken: cancellationToken);
        var response = await call.ResponseAsync.ConfigureAwait(false);
        CheckSchema(response);
        var result = response.ToModel();
        Log.ActionResult(_logger, actionName, result.Status, result.Message);
        return result;
    }

    /// <summary>Closes the underlying gRPC channel.</summary>
    public void Dispose() => _channel?.Dispose();

    private static bool IsValidRetryDelay(TimeSpan delay) =>
        delay > TimeSpan.Zero && delay.TotalMilliseconds <= int.MaxValue;

    /// <summary>Records and logs mismatches not seen before; returns them, or <see langword="null"/> when there are none.</summary>
    private List<SchemaMismatch>? CheckSchema(IMessage message)
    {
        if (!_detectSchemaMismatches) return null;

        List<SchemaMismatch>? added = null;
        foreach (var mismatch in SchemaInspector.Inspect(message))
        {
            if (!_seenMismatches.TryAdd(mismatch, 0)) continue;
            _mismatches.Enqueue(mismatch);
            Log.SchemaMismatch(_logger, TelemetrySchema.BundledGameVersion, mismatch);
            (added ??= []).Add(mismatch);
        }

        return added;
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Debug, "Connecting to Star Citizen telemetry at {Address}")]
        public static partial void Connecting(ILogger logger, string address);

        [LoggerMessage(2, LogLevel.Information, "Receiving Star Citizen telemetry from {Address}")]
        public static partial void Connected(ILogger logger, string address);

        [LoggerMessage(3, LogLevel.Information, "Star Citizen telemetry disconnected ({Reason}); retrying in {RetryDelay}")]
        public static partial void Disconnected(ILogger logger, string? reason, TimeSpan retryDelay);

        [LoggerMessage(4, LogLevel.Warning,
            "The game's telemetry does not match the bundled schema (StarCitizen.exe {BundledGameVersion}): {Mismatch}. The affected value is not mapped; update the schema.")]
        public static partial void SchemaMismatch(ILogger logger, Version bundledGameVersion, SchemaMismatch mismatch);

        [LoggerMessage(5, LogLevel.Debug, "Triggering action {ActionName} (hold {Hold})")]
        public static partial void TriggeringAction(ILogger logger, string actionName, TimeSpan? hold);

        [LoggerMessage(6, LogLevel.Debug, "Action {ActionName} returned {Status}: {Message}")]
        public static partial void ActionResult(ILogger logger, string actionName, ActionStatus status, string message);
    }
}
