using System.Text.RegularExpressions;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sc.Game.Telemetry.V1;

namespace StarCitizen.LocalTelemetry.Tests;

public class ClientFeatureTests
{
    [Fact]
    public async Task SchemaMismatchesAreReportedOnceAndLogged()
    {
        var invoker = new SingleStreamInvoker(FrameWithUnknownData(), FrameWithUnknownData());
        var logger = new CapturingLogger();
        using var client = new StarCitizenTelemetryClient(
            new GameTelemetryService.GameTelemetryServiceClient(invoker), logger: logger);

        var events = await client.ObserveTelemetryAsync(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken)
            .TakeWhile(e => e.Kind != TelemetryStreamEventKind.Disconnected)
            .ToListAsync(TestContext.Current.CancellationToken);

        var mismatches = events.Where(e => e.Kind == TelemetryStreamEventKind.SchemaMismatch).Select(e => e.Mismatch!).ToList();
        Assert.Equal(2, mismatches.Count);
        Assert.Contains(mismatches, m => m is { Kind: SchemaMismatchKind.UnknownField, Number: 99 });
        Assert.Contains(mismatches, m => m.Kind == SchemaMismatchKind.UnknownEnumValue && m.Number == 99 && m.Path.EndsWith(".mode", StringComparison.Ordinal));
        Assert.Equal(2, events.Count(e => e.Kind == TelemetryStreamEventKind.Frame));
        Assert.Equal(mismatches, client.SchemaMismatches);
        Assert.Equal(2, logger.Entries.Count(e => e.Level == LogLevel.Warning));
    }

    [Fact]
    public async Task SchemaDetectionCanBeDisabled()
    {
        var invoker = new SingleStreamInvoker(FrameWithUnknownData());
        using var client = new StarCitizenTelemetryClient(
            new GameTelemetryService.GameTelemetryServiceClient(invoker),
            options: new StarCitizenTelemetryOptions { DetectSchemaMismatches = false });

        var frames = await client.ReadTelemetryAsync(TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(frames);
        Assert.Empty(client.SchemaMismatches);
    }

    [Fact]
    public void BundledGameVersionMatchesProtoHeader()
    {
        var proto = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "StarCitizen.LocalTelemetry", "Protos", "sc_telemetry_types.proto"));
        var version = Regex.Match(proto, @"file version (\d+\.\d+\.\d+\.\d+)").Groups[1].Value;

        Assert.Equal(Version.Parse(version), TelemetrySchema.BundledGameVersion);
    }

    [Fact]
    public void DependencyInjectionRegistersOneConfiguredClient()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddStarCitizenTelemetry(o => o.Address = new Uri("http://127.0.0.1:12345"));
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<StarCitizenTelemetryClient>();
        Assert.Same(client, provider.GetRequiredService<ITelemetryClient>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void OptionsRejectNonPositiveRetryDelay(int milliseconds) =>
        Assert.Throws<ArgumentException>(() => new StarCitizenTelemetryClient(
            new StarCitizenTelemetryOptions { RetryDelay = TimeSpan.FromMilliseconds(milliseconds) }, null));

    [Fact]
    public async Task ExtensionsWorkWithAFakeClient()
    {
        var fake = new FakeTelemetryClient();

        await fake.SetLightsAsync(true, cancellationToken: TestContext.Current.CancellationToken);
        await fake.TriggerKnownActionAsync("v_invoke_ping", TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        Assert.Equal([(GameActions.LightsController.LightsOn, (TimeSpan?)null), ("v_invoke_ping", TimeSpan.FromMilliseconds(100))], fake.Triggered);
    }

    private static TelemetryStreamResponse FrameWithUnknownData()
    {
        var frame = new TelemetryStreamResponse
        {
            Vehicle = new VehicleFrame
            {
                State = new VehicleState { MasterMode = new VehicleMasterModeState { Mode = (VehicleMasterMode)99 } }
            }
        };
        var output = new MemoryStream();
        var writer = new CodedOutputStream(output);
        frame.WriteTo(writer);
        writer.WriteTag(99, WireFormat.WireType.Varint);
        writer.WriteInt32(1);
        writer.Flush();
        return TelemetryStreamResponse.Parser.ParseFrom(output.ToArray());
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "StarCitizen.LocalTelemetry.slnx"))) return dir.FullName;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class FakeTelemetryClient : ITelemetryClient
    {
        public List<(string Name, TimeSpan? Hold)> Triggered { get; } = [];

        public IAsyncEnumerable<TelemetryFrame> ReadTelemetryAsync(CancellationToken cancellationToken = default) =>
            AsyncEnumerable.Empty<TelemetryFrame>();

        public IAsyncEnumerable<TelemetryStreamEvent> ObserveTelemetryAsync(TimeSpan? retryDelay = null, CancellationToken cancellationToken = default) =>
            AsyncEnumerable.Empty<TelemetryStreamEvent>();

        public Task<IReadOnlyList<GameAction>> GetActionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GameAction>>([new GameAction("v_invoke_ping", "radar", true)]);

        public Task<ActionResult> TriggerActionAsync(string actionName, TimeSpan? hold = null, CancellationToken cancellationToken = default)
        {
            Triggered.Add((actionName, hold));
            return Task.FromResult(new ActionResult(ActionStatus.Ok, ""));
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class SingleStreamInvoker(params TelemetryStreamResponse[] frames) : CallInvoker
    {
        private bool _used;

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            if (_used) throw new RpcException(new Status(StatusCode.Unavailable, "gone"));
            _used = true;
            var reader = (IAsyncStreamReader<TResponse>)(object)new Reader(frames);
            return new(reader, Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();

        private sealed class Reader(TelemetryStreamResponse[] frames) : IAsyncStreamReader<TelemetryStreamResponse>
        {
            private int _index = -1;
            public TelemetryStreamResponse Current => frames[_index];

            public Task<bool> MoveNext(CancellationToken cancellationToken) =>
                Task.FromResult(++_index < frames.Length);
        }
    }
}
