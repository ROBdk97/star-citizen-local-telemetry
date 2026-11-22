using System.Text.RegularExpressions;

namespace StarCitizen.LocalTelemetry.Setup;

/// <summary>Finds the Star Citizen installation from the RSI Launcher logs or well-known folders.</summary>
public static class StarCitizenLocator
{
    private static readonly string[] KnownChannels = ["LIVE", "PTU", "EPTU", "TECH-PREVIEW", "HOTFIX"];

    private static readonly string[] DefaultInstallSubPaths =
    [
        Path.Combine("Roberts Space Industries", "StarCitizen"),
        "StarCitizen",
        Path.Combine("Games", "StarCitizen"),
        "SC"
    ];

    // "filePaths": ["C:\\..."]
    private static readonly Regex FilePathsRegexInstance = new(@"""filePaths"":\s*\[\s*""([^""]+)""\s*\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static Regex FilePathsRegex() => FilePathsRegexInstance;

    // Delta update completed (SC PTU 5.0.0-ptu.13248297) in C:\\Games\\StarCitizen\\PTU
    private static readonly Regex DeltaUpdateRegexInstance = new(@"\(SC (LIVE|PTU|TECH-PREVIEW|EPTU|HOTFIX) ([^\s)]+)\)\s+in\s+([A-Za-z]:\\.+?)(?=\s*\(|\s*""|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static Regex DeltaUpdateRegex() => DeltaUpdateRegexInstance;

    // Launching Star Citizen PTU from (C:\\Games\\StarCitizen\\PTU)
    private static readonly Regex LaunchRegexInstance = new(@"Launching Star Citizen (LIVE|PTU|TECH-PREVIEW|EPTU|HOTFIX) from \(([^)]+)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static Regex LaunchRegex() => LaunchRegexInstance;

    // Star Citizen LIVE 4.0.1 ... at C:\\...
    private static readonly Regex InstallerRegexInstance = new(@"Star Citizen (LIVE|PTU|TECH-PREVIEW|EPTU|HOTFIX) ([^\s]+).*? at ([A-Za-z]:\\.+?)(?=\s*\(|\s*""|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static Regex InstallerRegex() => InstallerRegexInstance;

    /// <summary>
    /// Finds the installation. Reads the RSI Launcher logs (<c>%APPDATA%\rsilauncher\logs</c>), newest entries
    /// first, then checks well-known folders on each fixed drive.
    /// </summary>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>
    /// The installation, or <see langword="null"/> if none was found; then ask the user for the folder and
    /// call <see cref="FromPath"/>.
    /// </returns>
    public static Task<StarCitizenInstallation?> FindAsync(CancellationToken cancellationToken = default) =>
        FindAsync(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "rsilauncher", "logs"),
            DefaultInstallationPaths(),
            cancellationToken);

    /// <summary>Reads the installation at a folder, for example one entered by the user.</summary>
    /// <param name="path">
    /// The installation folder, its parent, a channel folder (for example <c>...\StarCitizen\PTU</c>), or a
    /// file or folder inside a channel.
    /// </param>
    /// <param name="versions">Optional channel versions keyed by channel folder name.</param>
    /// <returns>The installation, or <see langword="null"/> if no channel was found.</returns>
    public static StarCitizenInstallation? FromPath(string path, IReadOnlyDictionary<string, string>? versions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (ResolveRoot(path.Trim().Trim('"')) is not { } root) return null;

        var channels = ChannelFolders(root)
            .OrderBy(d => Array.FindIndex(KnownChannels, c => c.Equals(d.Name, StringComparison.OrdinalIgnoreCase)) is var i and >= 0 ? i : int.MaxValue)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => new GameChannel(d.Name, d.FullName, versions?.GetValueOrDefault(d.Name)))
            .ToArray();

        return new StarCitizenInstallation(root.FullName, channels);
    }

    internal static async Task<StarCitizenInstallation?> FindAsync(
        string logDirectory,
        IEnumerable<string> fallbackPaths,
        CancellationToken cancellationToken)
    {
        var candidates = new List<string>();
        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(logDirectory))
        {
            foreach (var file in Directory.GetFiles(logDirectory, "log*.log").OrderByDescending(File.GetLastWriteTimeUtc))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var lines = await File.ReadAllLinesAsync(file, cancellationToken).ConfigureAwait(false);
                    ParseLogLines(lines, candidates, versions);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Locked or inaccessible; try the next log.
                }
            }
        }

        foreach (var candidate in candidates.Concat(fallbackPaths))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FromPath(candidate, versions) is { } installation) return installation;
        }

        return null;
    }

    /// <summary>Collects install paths (newest first) and the newest version per channel.</summary>
    internal static void ParseLogLines(IReadOnlyList<string> lines, List<string> candidates, Dictionary<string, string> versions)
    {
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (!TryParseLogLine(lines[i], out var path, out var channel, out var version)) continue;

            var resolved = path;
            if (channel.Length > 0 && !Path.GetFileName(path.TrimEnd('\\', '/')).Equals(channel, StringComparison.OrdinalIgnoreCase))
                resolved = Path.Combine(path, channel);

            if (!candidates.Contains(resolved, StringComparer.OrdinalIgnoreCase)) candidates.Add(resolved);
            if (channel.Length > 0 && version.Length > 0) versions.TryAdd(channel.ToUpperInvariant(), version);
        }
    }

    private static bool TryParseLogLine(string line, out string path, out string channel, out string version)
    {
        (path, channel, version) = (string.Empty, string.Empty, string.Empty);
        if (FilePathsRegex().Match(line) is { Success: true } filePaths)
        {
            path = Normalize(filePaths.Groups[1].Value);
        }
        else if (DeltaUpdateRegex().Match(line) is { Success: true } delta)
        {
            (path, channel, version) = (Normalize(delta.Groups[3].Value), delta.Groups[1].Value, delta.Groups[2].Value);
        }
        else if (LaunchRegex().Match(line) is { Success: true } launch)
        {
            (path, channel) = (Normalize(launch.Groups[2].Value), launch.Groups[1].Value);
        }
        else if (InstallerRegex().Match(line) is { Success: true } installer)
        {
            (path, channel, version) = (Normalize(installer.Groups[3].Value), installer.Groups[1].Value, installer.Groups[2].Value);
        }

        return path.Length > 0;

        static string Normalize(string value) => value.Trim().Replace(@"\\", @"\");
    }

    private static DirectoryInfo? ResolveRoot(string path)
    {
        DirectoryInfo start;
        try
        {
            var full = Path.GetFullPath(path);
            start = new DirectoryInfo(File.Exists(full) ? Path.GetDirectoryName(full)! : full);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return null;
        }

        if (!start.Exists) return null;
        if (!IsChannel(start) && !ChannelFolders(start).Any())
        {
            var nested = new DirectoryInfo(Path.Combine(start.FullName, "StarCitizen"));
            if (nested.Exists && ChannelFolders(nested).Any()) return nested;
        }

        for (var dir = start; dir is not null; dir = dir.Parent)
        {
            if (IsChannel(dir)) return dir.Parent;
            if (ChannelFolders(dir).Any()) return dir;
        }

        return null;
    }

    private static IEnumerable<DirectoryInfo> ChannelFolders(DirectoryInfo root)
    {
        try
        {
            return root.EnumerateDirectories().Where(IsChannel).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static bool IsChannel(DirectoryInfo dir) =>
        File.Exists(Path.Combine(dir.FullName, "Bin64", "StarCitizen.exe")) ||
        (KnownChannels.Contains(dir.Name, StringComparer.OrdinalIgnoreCase) && dir.Parent is not null &&
         Directory.Exists(Path.Combine(dir.FullName, "Bin64")));

    private static IEnumerable<string> DefaultInstallationPaths()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Roberts Space Industries", "StarCitizen");
        foreach (var drive in DriveInfo.GetDrives().Where(d => d is { DriveType: DriveType.Fixed, IsReady: true }))
        {
            foreach (var sub in DefaultInstallSubPaths) yield return Path.Combine(drive.Name, sub);
        }
    }
}
