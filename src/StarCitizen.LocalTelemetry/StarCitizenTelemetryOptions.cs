namespace StarCitizen.LocalTelemetry;

/// <summary>Settings for <see cref="StarCitizenTelemetryClient"/>.</summary>
public sealed class StarCitizenTelemetryOptions
{
    /// <summary>The service address; defaults to <see cref="StarCitizenTelemetryClient.DefaultAddress"/>. The service is plaintext HTTP/2.</summary>
    public Uri Address { get; set; } = StarCitizenTelemetryClient.DefaultAddress;

    /// <summary>
    /// The delay before reconnecting when <see cref="ITelemetryClient.ObserveTelemetryAsync"/> is called without one.
    /// Defaults to 2 seconds; must be positive.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Whether to check each response against the bundled schema and report <see cref="SchemaMismatch"/>es.
    /// Defaults to <see langword="true"/>; disabling it saves a re-serialization per frame.
    /// </summary>
    public bool DetectSchemaMismatches { get; set; } = true;
}
