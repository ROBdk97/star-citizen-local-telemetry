# Setup

The game's telemetry service needs two settings:

- the user environment variable `GRPC_CLIENT_SERVER_ENABLED=true`, read when the
  RSI Launcher starts;
- `server.game.telemetry.enabled=1` in the channel's `user.cfg`, for example
  `...\StarCitizen\PTU\user.cfg`.

`TelemetrySetup` in the `StarCitizen.LocalTelemetry.Setup` namespace sets both.
Restart the RSI Launcher and the game afterwards.

```csharp
using StarCitizen.LocalTelemetry.Setup;

// Find the game, then enable telemetry for PTU.
var result = await TelemetrySetup.EnableAsync("PTU");
Console.WriteLine(result.RestartRequired ? "Restart the launcher and game." : "Already enabled.");

// Show each channel's state.
foreach (var status in await TelemetrySetup.GetStatusAsync())
    Console.WriteLine($"{status.Channel.Name} {status.Channel.Version}: {(status.IsEnabled ? "enabled" : "disabled")}");

// Disable again; pass removeEnvironmentVariable: true to also remove the variable (affects every channel).
await TelemetrySetup.DisableAsync("PTU");
```

All three accept an `installPath` for when the game can't be found automatically.
`EnableAsync` and `DisableAsync` throw `DirectoryNotFoundException` when no
installation is found, and `ArgumentException` when the channel isn't installed.
The message lists the installed channels.

## Finding the game

`StarCitizenLocator.FindAsync()` reads the RSI Launcher logs
(`%APPDATA%\rsilauncher\logs\log*.log`), newest entries first. It picks up the
install folder and each channel's version. If the logs give no result, it checks
well-known folders such as `C:\Program Files\Roberts Space Industries\StarCitizen`
and `<drive>:\StarCitizen` on each fixed drive. It returns `null` if nothing is
found.

`StarCitizenLocator.FromPath(path)` accepts the StarCitizen folder, its parent,
a channel folder, or a file or folder inside a channel (such as `Bin64`). Use it
for a folder entered by the user. A `GameChannel` exposes `UserCfgPath` and
`ExecutablePath`.

## How user.cfg is edited

- An existing `server.game.telemetry.enabled` line is updated in place.
  Otherwise one is appended. Commented lines are ignored.
- Other lines, line endings and the encoding (including a BOM) are kept.
- Before the first change, the original is copied to `user.cfg.bak`.
- Disabling never creates a file or adds a line.

The environment-variable methods are Windows-only. `ReadUserCfg` and
`WriteUserCfg` work with any file path.
