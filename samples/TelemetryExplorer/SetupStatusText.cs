using StarCitizen.LocalTelemetry;
using StarCitizen.LocalTelemetry.Setup;

namespace TelemetryExplorer;

/// <summary>UI-independent text for the Setup tab.</summary>
internal static class SetupStatusText
{
    public static string DescribeState(TelemetrySetupStatus status) => status.UserCfgEnabled switch
    {
        false => "Configured telemetry: disabled",
        true when status.EnvironmentVariableEnabled => "Configured telemetry: enabled",
        true => "Configured telemetry: incomplete (environment variable not enabled)",
        null => "Configured telemetry: unknown (not set in user.cfg)",
    };

    public static string Describe(TelemetrySetupStatus status, GameVersionCheck version)
    {
        var cfg = status.UserCfgEnabled switch
        {
            true => "enabled",
            false => "disabled",
            null => "not set",
        };
        var game = version.GameVersion is { } v
            ? version.IsMatch ? $"{v} (matches the bundled schema)" : $"{v} (schema is for {version.BundledGameVersion}; differences are reported as schema mismatches)"
            : "StarCitizen.exe not found";

        return string.Join(Environment.NewLine,
            $"Channel: {status.Channel.Name}{(status.Channel.Version is { } cv ? $" ({cv})" : "")}",
            $"Folder: {status.Channel.FolderPath}",
            $"{TelemetrySetup.EnvironmentVariable}: {(status.EnvironmentVariableEnabled ? "enabled" : "not set")}",
            $"{TelemetrySetup.ConsoleVariable} in user.cfg: {cfg}",
            $"Game version: {game}",
            status.IsEnabled ? "Telemetry is configured." : "Telemetry is not configured.");
    }

    public static string Describe(string action, TelemetrySetupResult result)
    {
        if (!result.RestartRequired) return $"{action}: nothing to change.";
        var changes = new List<string>();
        if (result.EnvironmentVariableChanged) changes.Add("environment variable");
        if (result.UserCfgChanged) changes.Add("user.cfg");
        var backup = result.BackupPath is { } path ? $" Backup: {path}." : "";
        return $"{action}: changed {string.Join(" and ", changes)}.{backup} Restart the RSI Launcher and the game.";
    }
}
