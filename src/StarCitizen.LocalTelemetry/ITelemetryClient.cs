namespace StarCitizen.LocalTelemetry;

/// <summary>
/// The operations of Star Citizen's local telemetry service. <see cref="StarCitizenTelemetryClient"/> implements it;
/// implement it yourself to fake the game in tests.
/// </summary>
/// <remarks>
/// Helpers such as <see cref="TelemetryClientExtensions.ReadTelemetryWithReconnectAsync"/>,
/// <see cref="TelemetryClientExtensions.TriggerKnownActionAsync"/> and the <see cref="GameActionExtensions"/>
/// are extension methods over this interface, so they work with any implementation.
/// </remarks>
public interface ITelemetryClient
{
    /// <summary>Streams telemetry until the game ends the stream or the call fails.</summary>
    /// <param name="cancellationToken">Stops the stream.</param>
    /// <returns>The frames in the order they were received.</returns>
    /// <exception cref="Grpc.Core.RpcException">The call failed, for example with <see cref="Grpc.Core.StatusCode.Unavailable"/> when the game isn't running.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    IAsyncEnumerable<TelemetryFrame> ReadTelemetryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams telemetry, reconnects when the game is unavailable or ends the stream, and reports
    /// connection changes and schema mismatches.
    /// </summary>
    /// <param name="retryDelay">The delay before reconnecting; the client's default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the stream.</param>
    /// <returns>Connection, schema and frame events until canceled.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="retryDelay"/> is not positive.</exception>
    /// <exception cref="Grpc.Core.RpcException">The call failed with an error other than <see cref="Grpc.Core.StatusCode.Unavailable"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    IAsyncEnumerable<TelemetryStreamEvent> ObserveTelemetryAsync(
        TimeSpan? retryDelay = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the actions the running game currently advertises.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The advertised actions.</returns>
    Task<IReadOnlyList<GameAction>> GetActionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends one action to the game. Never retried: if the call fails in transit, whether the game
    /// performed the action is unknown.
    /// </summary>
    /// <param name="actionName">An action name, for example <see cref="GameActions.LightsController.LightsOn"/>.</param>
    /// <param name="hold">How long to hold the action; only valid for holdable actions.</param>
    /// <param name="cancellationToken">Cancels the request; the game may still have performed the action.</param>
    /// <returns>The game's response; check <see cref="ActionResult.IsSuccess"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="actionName"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="hold"/> is negative or too large.</exception>
    Task<ActionResult> TriggerActionAsync(
        string actionName,
        TimeSpan? hold = null,
        CancellationToken cancellationToken = default);
}
