namespace StarCitizen.LocalTelemetry;

/// <summary>The sections of a <see cref="TelemetryFrame"/>, as flags.</summary>
[Flags]
public enum TelemetrySections
{
    /// <summary>No section.</summary>
    None = 0,
    /// <summary><see cref="TelemetryFrame.Vehicle"/>.</summary>
    Vehicle = 1,
    /// <summary><see cref="TelemetryFrame.Weapons"/>.</summary>
    Weapons = 2,
    /// <summary><see cref="TelemetryFrame.Events"/>.</summary>
    Events = 4,
    /// <summary><see cref="TelemetryFrame.Actor"/>.</summary>
    Actor = 8,
    /// <summary><see cref="TelemetryFrame.ForceFeedback"/>.</summary>
    ForceFeedback = 16,
    /// <summary><see cref="TelemetryFrame.Turret"/>.</summary>
    Turret = 32
}

/// <summary>A frame yielded by <see cref="TelemetryStreamExtensions.ObserveChanges"/>.</summary>
/// <param name="Sections">The sections that changed compared with the previous value of each section.</param>
/// <param name="Frame">The original frame; it is not merged with earlier frames.</param>
public sealed record TelemetryChange(TelemetrySections Sections, TelemetryFrame Frame);
