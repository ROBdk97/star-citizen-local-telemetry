namespace StarCitizen.LocalTelemetry.Setup;

/// <summary>A Star Citizen installation: the folder that contains the channel folders.</summary>
/// <param name="RootPath">The installation folder, for example <c>C:\Program Files\Roberts Space Industries\StarCitizen</c>.</param>
/// <param name="Channels">The installed channels, LIVE first.</param>
public sealed record StarCitizenInstallation(string RootPath, IReadOnlyList<GameChannel> Channels)
{
    /// <summary>Finds a channel by folder name, ignoring case.</summary>
    /// <param name="name">The channel folder name, for example <c>PTU</c>.</param>
    /// <returns>The channel, or <see langword="null"/> if it isn't installed.</returns>
    public GameChannel? FindChannel(string name) =>
        Channels.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>An installed game channel, such as LIVE or PTU.</summary>
/// <param name="Name">The channel folder name, for example <c>PTU</c>.</param>
/// <param name="FolderPath">The channel folder.</param>
/// <param name="Version">The version from the RSI Launcher log, or <see langword="null"/> when unknown.</param>
public sealed record GameChannel(string Name, string FolderPath, string? Version)
{
    /// <summary>The channel's <c>user.cfg</c>, which may not exist yet.</summary>
    public string UserCfgPath => Path.Combine(FolderPath, "user.cfg");

    /// <summary>The game executable, <c>Bin64\StarCitizen.exe</c>.</summary>
    public string ExecutablePath => Path.Combine(FolderPath, "Bin64", "StarCitizen.exe");
}
