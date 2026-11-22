using System.Diagnostics;
using StarCitizen.LocalTelemetry.Setup;

namespace StarCitizen.LocalTelemetry;

/// <summary>Information about the protobuf schema bundled with this library.</summary>
public static class TelemetrySchema
{
    /// <summary>
    /// The <c>StarCitizen.exe</c> file version the bundled schema was extracted from. Other builds often use the same
    /// schema; the client reports actual differences as <see cref="SchemaMismatch"/>es.
    /// </summary>
    // Updated by tools/Update-Proto.ps1.
    public static Version BundledGameVersion { get; } = new(5, 0, 202, 10025);

    /// <summary>Compares a channel's <c>StarCitizen.exe</c> file version with <see cref="BundledGameVersion"/>.</summary>
    /// <param name="channel">The installed channel, for example from <see cref="StarCitizenLocator.FindAsync(CancellationToken)"/>.</param>
    /// <returns>The comparison; <see cref="GameVersionCheck.GameVersion"/> is <see langword="null"/> when the executable is missing.</returns>
    public static GameVersionCheck CheckGameVersion(GameChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        Version? version = null;
        if (File.Exists(channel.ExecutablePath))
        {
            var info = FileVersionInfo.GetVersionInfo(channel.ExecutablePath);
            version = new(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
        }

        return new(BundledGameVersion, version);
    }
}

/// <summary>The result of <see cref="TelemetrySchema.CheckGameVersion"/>.</summary>
/// <param name="BundledGameVersion">The game version the bundled schema was extracted from.</param>
/// <param name="GameVersion">The installed <c>StarCitizen.exe</c> file version, or <see langword="null"/> when it was not found.</param>
public sealed record GameVersionCheck(Version BundledGameVersion, Version? GameVersion)
{
    /// <summary>Whether the installed game is the build the schema was extracted from.</summary>
    public bool IsMatch => GameVersion == BundledGameVersion;
}
