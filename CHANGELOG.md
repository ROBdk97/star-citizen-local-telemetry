# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/) (see [README.md](README.md#versioning)).

When releasing, rename `Unreleased` to the version being tagged, for example
`## [0.1.0-preview.1] - 2025-01-31`. The release workflow uses that section as the
GitHub release notes.

## [0.2.0-preview.1] - 2026-11-22

### Added

- `GameToggles.Known`, a catalog of state-aware on/off action pairs.

## [0.1.0-preview.1] - 2026-11-22

Schema: `StarCitizen.exe` 5.0.202.10025 (PTU). Actions: 112, from the live game.

### Added

- `StarCitizenTelemetryClient` for the game's local gRPC telemetry at
  `http://127.0.0.1:2942`, with C# records and enums instead of protobuf types.
- `ReadTelemetryAsync`, `ReadTelemetryWithReconnectAsync`, `ObserveTelemetryAsync`
  (connection, frame, disconnect and schema events) and the `Vehicles()`,
  `Actors()`, `Weapons()`, `GameEvents()` and `ObserveChanges()` stream helpers.
- `GameActions` constants for every advertised action, `GetActionsAsync`,
  `FindActionsAsync`, `TriggerActionAsync`, `TriggerKnownActionAsync` and typed
  action helpers such as `SetLightsAsync`.
- `ITelemetryClient`, so apps can replace the client in tests. The helpers are
  extension methods on the interface.
- `StarCitizenTelemetryOptions`, `ILogger` support and
  `services.AddStarCitizenTelemetry()` for dependency injection.
- Schema mismatch detection (`SchemaMismatch`, `client.SchemaMismatches`) and
  `TelemetrySchema.CheckGameVersion`.
- `StarCitizen.LocalTelemetry.Setup`: `StarCitizenLocator` and `TelemetrySetup`
  to find installed channels and enable or disable telemetry.
- Targets `netstandard2.1` and `net11.0`; the net11.0 build is trim- and
  native-AOT-compatible. The netstandard2.1 build depends only on the 8.0
  `Microsoft.Extensions.*.Abstractions` and `Microsoft.Bcl.TimeProvider` packages,
  so net8.0 and net9.0 apps aren't forced onto the 11.0 packages.
- `tools\Update-Proto.ps1` downloads a pinned sc_make_proto and its Python
  environment when run without parameters.
