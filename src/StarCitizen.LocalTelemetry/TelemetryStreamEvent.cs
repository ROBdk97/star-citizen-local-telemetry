namespace StarCitizen.LocalTelemetry;

/// <summary>The kind of a <see cref="TelemetryStreamEvent"/>.</summary>
public enum TelemetryStreamEventKind
{
    /// <summary>A connection attempt is starting.</summary>
    Connecting,
    /// <summary>The first frame of this connection arrived.</summary>
    Connected,
    /// <summary>A telemetry frame arrived; see <see cref="TelemetryStreamEvent.Frame"/>.</summary>
    Frame,
    /// <summary>The connection ended; see <see cref="TelemetryStreamEvent.Reason"/>. A retry follows after the retry delay.</summary>
    Disconnected,
    /// <summary>
    /// The next frame contains data the bundled schema doesn't describe; see <see cref="TelemetryStreamEvent.Mismatch"/>.
    /// Reported once per distinct mismatch.
    /// </summary>
    SchemaMismatch
}

/// <summary>An event yielded by <see cref="ITelemetryClient.ObserveTelemetryAsync"/>.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="Frame">Set for <see cref="TelemetryStreamEventKind.Frame"/>.</param>
/// <param name="Reason">
/// Set for <see cref="TelemetryStreamEventKind.Disconnected"/>: <c>Unavailable</c> (optionally followed by the
/// gRPC detail) or <c>Stream ended</c>.
/// </param>
/// <param name="Mismatch">Set for <see cref="TelemetryStreamEventKind.SchemaMismatch"/>.</param>
public sealed record TelemetryStreamEvent(
    TelemetryStreamEventKind Kind,
    TelemetryFrame? Frame = null,
    string? Reason = null,
    SchemaMismatch? Mismatch = null);