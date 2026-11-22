# Telemetry Explorer

A Windows desktop app for exploring the telemetry and triggering actions. It's
built with [MewUI](https://github.com/aprillz/MewUI).

```powershell
dotnet run --project .\samples\TelemetryExplorer\TelemetryExplorer.csproj
dotnet run --project .\samples\TelemetryExplorer\TelemetryExplorer.csproj -- --address http://127.0.0.1:2942
```

It reconnects automatically, so you can start it before the game.

## Tabs

- **Telemetry**: a tree of sections (Vehicle, Weapons, Actor, ForceFeedback, Turret)
  and a filterable, sortable grid of every field. The grid shows each field's
  value, type and the time it last changed. Sections keep their last values,
  because the game sends partial frames.
- **Actions**: every action the game advertises, with search and category
  filters. If discovery fails, it shows the built-in `GameActions.Known` catalog
  instead. Select an action and press **Trigger**, or double-click it. The action is
  sent immediately, without confirmation. A hold duration is available only for holdable
  actions.
- **Log**: connection changes, game events and action results (latest 500).
- **Raw JSON**: the last value of each section.
- **Setup**: finds the Star Citizen installation (or use **Use folder** with the
  folder that contains `LIVE`/`PTU`), shows each channel's telemetry configuration
  and `StarCitizen.exe` version, and enables or disables telemetry with
  `TelemetrySetup` (environment variable plus `user.cfg`, which is backed up
  first). PTU is selected by default. Restart the RSI Launcher and the game afterwards.

The status bar shows the connection state, frame count and age of the last frame.

Triggering an action changes the game. A failed call doesn't prove that the game
didn't apply the action; check the telemetry.

## Code

- `TelemetryModel.cs`: UI-independent snapshot state. It flattens fields with
  reflection and tracks when each one last changed. Unit tests cover it.
- `SetupStatusText.cs`: the Setup tab's status and result text. Unit tests cover it.
- `MainWindow.cs`: the MewUI layout. It polls the model every 250 ms.
