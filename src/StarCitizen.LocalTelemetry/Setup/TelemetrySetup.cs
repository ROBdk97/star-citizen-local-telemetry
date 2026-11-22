using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;

namespace StarCitizen.LocalTelemetry.Setup;

/// <summary>
/// Enables or disables the game's local telemetry service. This needs the user environment variable
/// <see cref="EnvironmentVariable"/> set to <c>true</c>, and <see cref="ConsoleVariable"/> set to <c>1</c> in the
/// channel's <c>user.cfg</c>. Restart the RSI Launcher and the game afterwards. The environment variable
/// methods are Windows-only; the <c>user.cfg</c> methods work anywhere.
/// </summary>
public static class TelemetrySetup
{
    /// <summary>The environment variable that enables the game's gRPC server.</summary>
    public const string EnvironmentVariable = "GRPC_CLIENT_SERVER_ENABLED";

    /// <summary>The console variable that enables telemetry.</summary>
    public const string ConsoleVariable = "server.game.telemetry.enabled";

    private static readonly Regex CvarLineRegexInstance = new(@"^(?<lead>\s*)server\.game\.telemetry\.enabled(?<sep>\s*=\s*|\s+)(?<value>[^\s;#]*)(?<rest>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static Regex CvarLineRegex() => CvarLineRegexInstance;

    /// <summary>
    /// Finds the installation and enables telemetry for a channel: sets <see cref="EnvironmentVariable"/> for the
    /// user and <see cref="ConsoleVariable"/><c>=1</c> in the channel's <c>user.cfg</c>.
    /// </summary>
    /// <param name="channelName">The channel folder name, for example <c>PTU</c> or <c>LIVE</c>.</param>
    /// <param name="installPath">
    /// The StarCitizen folder, or <see langword="null"/> to find it with <see cref="StarCitizenLocator.FindAsync(CancellationToken)"/>.
    /// </param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>What was changed. Restart the RSI Launcher and the game when <see cref="TelemetrySetupResult.RestartRequired"/>.</returns>
    /// <exception cref="DirectoryNotFoundException">No installation was found.</exception>
    /// <exception cref="ArgumentException">The channel isn't installed.</exception>
    [SupportedOSPlatform("windows")]
    public static async Task<TelemetrySetupResult> EnableAsync(string channelName, string? installPath = null,
        CancellationToken cancellationToken = default) =>
        Enable(await ResolveChannelAsync(channelName, installPath, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Finds the installation and disables telemetry for a channel by setting <see cref="ConsoleVariable"/><c>=0</c>.
    /// </summary>
    /// <param name="channelName">The channel folder name, for example <c>PTU</c> or <c>LIVE</c>.</param>
    /// <param name="installPath">The StarCitizen folder, or <see langword="null"/> to find it.</param>
    /// <param name="removeEnvironmentVariable">Also removes the user environment variable, which affects every channel.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>What was changed.</returns>
    /// <exception cref="DirectoryNotFoundException">No installation was found.</exception>
    /// <exception cref="ArgumentException">The channel isn't installed.</exception>
    [SupportedOSPlatform("windows")]
    public static async Task<TelemetrySetupResult> DisableAsync(string channelName, string? installPath = null,
        bool removeEnvironmentVariable = false, CancellationToken cancellationToken = default) =>
        Disable(await ResolveChannelAsync(channelName, installPath, cancellationToken).ConfigureAwait(false),
            removeEnvironmentVariable);

    /// <summary>Finds the installation and reads the telemetry configuration of every channel.</summary>
    /// <param name="installPath">The StarCitizen folder, or <see langword="null"/> to find it.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The status of each installed channel.</returns>
    /// <exception cref="DirectoryNotFoundException">No installation was found.</exception>
    [SupportedOSPlatform("windows")]
    public static async Task<IReadOnlyList<TelemetrySetupStatus>> GetStatusAsync(string? installPath = null,
        CancellationToken cancellationToken = default)
    {
        var installation = await FindInstallationAsync(installPath, cancellationToken).ConfigureAwait(false);
        return [.. installation.Channels.Select(GetStatus)];
    }

    internal static async Task<GameChannel> ResolveChannelAsync(string channelName, string? installPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);
        var installation = await FindInstallationAsync(installPath, cancellationToken).ConfigureAwait(false);
        return installation.FindChannel(channelName) ?? throw new ArgumentException(
            $"Channel '{channelName}' isn't installed in '{installation.RootPath}'. Installed: " +
            string.Join(", ", installation.Channels.Select(c => c.Name)) + ".", nameof(channelName));
    }

    private static async Task<StarCitizenInstallation> FindInstallationAsync(string? installPath,
        CancellationToken cancellationToken)
    {
        var installation = installPath is null
            ? await StarCitizenLocator.FindAsync(cancellationToken).ConfigureAwait(false)
            : StarCitizenLocator.FromPath(installPath);
        return installation ?? throw new DirectoryNotFoundException(installPath is null
            ? "Star Citizen wasn't found in the RSI Launcher logs or the usual folders; pass installPath."
            : $"No Star Citizen channel was found in '{installPath}'.");
    }

    /// <summary>Reads the current configuration of a channel.</summary>
    /// <param name="channel">The channel to check.</param>
    /// <returns>The status.</returns>
    [SupportedOSPlatform("windows")]
    public static TelemetrySetupStatus GetStatus(GameChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        return new(channel, IsEnvironmentVariableEnabled(), ReadUserCfg(channel.UserCfgPath));
    }

    /// <summary>Sets the environment variable and enables the console variable in the channel's <c>user.cfg</c>.</summary>
    /// <param name="channel">The channel to enable telemetry for.</param>
    /// <returns>What was changed.</returns>
    [SupportedOSPlatform("windows")]
    public static TelemetrySetupResult Enable(GameChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        var environmentChanged = SetEnvironmentVariable(true);
        var cfg = WriteUserCfg(channel.UserCfgPath, true);
        return new(environmentChanged, cfg.Changed, cfg.BackupPath);
    }

    /// <summary>Disables the console variable in the channel's <c>user.cfg</c>.</summary>
    /// <param name="channel">The channel to disable telemetry for.</param>
    /// <param name="removeEnvironmentVariable">
    /// Also removes the user environment variable. It applies to every channel, so the default keeps it.
    /// </param>
    /// <returns>What was changed.</returns>
    [SupportedOSPlatform("windows")]
    public static TelemetrySetupResult Disable(GameChannel channel, bool removeEnvironmentVariable = false)
    {
        ArgumentNullException.ThrowIfNull(channel);
        var environmentChanged = removeEnvironmentVariable && SetEnvironmentVariable(false);
        var cfg = WriteUserCfg(channel.UserCfgPath, false);
        return new(environmentChanged, cfg.Changed, cfg.BackupPath);
    }

    /// <summary>Whether <see cref="EnvironmentVariable"/> is <c>true</c> for the user or the machine.</summary>
    /// <returns><see langword="true"/> when the variable is set to <c>true</c>.</returns>
    [SupportedOSPlatform("windows")]
    public static bool IsEnvironmentVariableEnabled() =>
        IsTrue(Environment.GetEnvironmentVariable(EnvironmentVariable, EnvironmentVariableTarget.User)) ||
        IsTrue(Environment.GetEnvironmentVariable(EnvironmentVariable, EnvironmentVariableTarget.Machine));

    /// <summary>Sets or removes <see cref="EnvironmentVariable"/> for the current user.</summary>
    /// <param name="enabled"><see langword="true"/> to set it to <c>true</c>; <see langword="false"/> to remove it.</param>
    /// <returns>Whether the user variable was changed.</returns>
    /// <remarks>Running programs, including the RSI Launcher, keep their old environment until restarted.</remarks>
    [SupportedOSPlatform("windows")]
    public static bool SetEnvironmentVariable(bool enabled)
    {
        var current = Environment.GetEnvironmentVariable(EnvironmentVariable, EnvironmentVariableTarget.User);
        if (enabled ? IsTrue(current) : current is null) return false;
        Environment.SetEnvironmentVariable(EnvironmentVariable, enabled ? "true" : null, EnvironmentVariableTarget.User);
        return true;
    }

    /// <summary>Reads <see cref="ConsoleVariable"/> from a <c>user.cfg</c> file.</summary>
    /// <param name="userCfgPath">The file path.</param>
    /// <returns>
    /// The last value in the file, or <see langword="null"/> when the file or the variable doesn't exist.
    /// Commented lines are ignored.
    /// </returns>
    public static bool? ReadUserCfg(string userCfgPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userCfgPath);
        if (!File.Exists(userCfgPath)) return null;

        bool? value = null;
        foreach (var line in File.ReadLines(userCfgPath))
        {
            if (CvarLineRegex().Match(line) is { Success: true } match) value = IsTrue(match.Groups["value"].Value);
        }

        return value;
    }

    /// <summary>
    /// Sets <see cref="ConsoleVariable"/> in a <c>user.cfg</c> file. Existing lines are updated in place and
    /// other lines, line endings and encoding are kept; otherwise the line is appended. Disabling a file
    /// without the variable changes nothing.
    /// </summary>
    /// <param name="userCfgPath">The file path. The file is created when enabling and it doesn't exist.</param>
    /// <param name="enabled">The value to write: <c>1</c> or <c>0</c>.</param>
    /// <returns>Whether the file changed, and the backup made before the first change.</returns>
    public static UserCfgChange WriteUserCfg(string userCfgPath, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userCfgPath);
        var value = enabled ? "1" : "0";

        if (!File.Exists(userCfgPath))
        {
            if (!enabled) return new(false, null);
            File.WriteAllText(userCfgPath, $"{ConsoleVariable}={value}\r\n");
            return new(true, null);
        }

        var bytes = File.ReadAllBytes(userCfgPath);
        var hasBom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        var text = new UTF8Encoding(false).GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));
        var newline = text.IndexOf("\r\n", StringComparison.Ordinal) >= 0 || text.IndexOf('\n') < 0 ? "\r\n" : "\n";

        var lines = text.Split('\n');
        var found = false;
        var changed = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (CvarLineRegex().Match(line) is not { Success: true } match) continue;

            found = true;
            if (match.Groups["value"].Value == value) continue;
            var updated = $"{match.Groups["lead"].Value}{ConsoleVariable}{match.Groups["sep"].Value}{value}{match.Groups["rest"].Value}";
            lines[i] = lines[i].EndsWith("\r", StringComparison.Ordinal) ? updated + "\r" : updated;
            changed = true;
        }

        if (!found)
        {
            if (!enabled) return new(false, null);
            var separator = text.Length == 0 || text.EndsWith("\n", StringComparison.Ordinal) ? "" : newline;
            text = $"{text}{separator}{ConsoleVariable}={value}{newline}";
            changed = true;
        }
        else if (changed)
        {
            text = string.Join("\n", lines);
        }

        if (!changed) return new(false, null);

        var backupPath = userCfgPath + ".bak";
        if (!File.Exists(backupPath)) File.Copy(userCfgPath, backupPath);
        else backupPath = null;

        var encoding = new UTF8Encoding(hasBom);
        File.WriteAllBytes(userCfgPath, [.. encoding.GetPreamble(), .. encoding.GetBytes(text)]);
        return new(true, backupPath);
    }

    private static bool IsTrue(string? value) =>
        value is not null && (value.Trim() == "1" || value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));
}

/// <summary>The telemetry configuration of a channel.</summary>
/// <param name="Channel">The channel.</param>
/// <param name="EnvironmentVariableEnabled">Whether <see cref="TelemetrySetup.EnvironmentVariable"/> is <c>true</c>.</param>
/// <param name="UserCfgEnabled">
/// The value of <see cref="TelemetrySetup.ConsoleVariable"/> in <c>user.cfg</c>, or <see langword="null"/> when it isn't set.
/// </param>
public sealed record TelemetrySetupStatus(GameChannel Channel, bool EnvironmentVariableEnabled, bool? UserCfgEnabled)
{
    /// <summary>Whether both settings are enabled.</summary>
    public bool IsEnabled => EnvironmentVariableEnabled && UserCfgEnabled == true;
}

/// <summary>What <see cref="TelemetrySetup.Enable"/> or <see cref="TelemetrySetup.Disable"/> changed.</summary>
/// <param name="EnvironmentVariableChanged">Whether the user environment variable changed; restart the RSI Launcher.</param>
/// <param name="UserCfgChanged">Whether <c>user.cfg</c> changed; restart the game.</param>
/// <param name="BackupPath">The backup of the original <c>user.cfg</c>, if one was made by this call.</param>
public sealed record TelemetrySetupResult(bool EnvironmentVariableChanged, bool UserCfgChanged, string? BackupPath)
{
    /// <summary>Whether anything changed, so the launcher and game must be restarted.</summary>
    public bool RestartRequired => EnvironmentVariableChanged || UserCfgChanged;
}

/// <summary>The result of <see cref="TelemetrySetup.WriteUserCfg"/>.</summary>
/// <param name="Changed">Whether the file changed.</param>
/// <param name="BackupPath">The backup made before changing the file, or <see langword="null"/> if none was made.</param>
public sealed record UserCfgChange(bool Changed, string? BackupPath);
