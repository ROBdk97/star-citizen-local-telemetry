# Contributing

This is an experimental, unofficial project. Issues and pull requests are welcome.

## Prerequisites

- Windows (the Telemetry Explorer sample, which the tests reference, is a Windows app).
- The .NET 11 SDK.
- Star Citizen with local telemetry enabled, only for updating the schema or action catalog
  and for manual testing. The tests don't need the game.

## Build, test and format

```powershell
dotnet build .\StarCitizen.LocalTelemetry.slnx -c Release
dotnet test --solution .\StarCitizen.LocalTelemetry.slnx -c Release
dotnet test --solution .\StarCitizen.LocalTelemetry.slnx -c Release --filter-method "*SchemaMismatchesAreReportedOnceAndLogged"
dotnet format .\StarCitizen.LocalTelemetry.slnx --verify-no-changes
```

CI runs the same build, format check, tests and pack for every pull request.
The library builds with warnings as errors where they matter: every public member
needs XML documentation, and the public API must match the API files below.

## Rules for tests

- Tests must never trigger a gameplay action in the real game, and must not change
  the game's `user.cfg` or environment variables. Use the fake call invoker in
  `StarCitizenTelemetryClientTests` or the in-process gRPC service in
  `GrpcIntegrationTests`.
- Add tests for behaviour you add or fix, preferably in the existing test files.

## Public API

`src\StarCitizen.LocalTelemetry\PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`
list the public API, checked by `Microsoft.CodeAnalysis.PublicApiAnalyzers`. After
adding, removing or changing a public member, update the files with:

```powershell
dotnet format analyzers .\src\StarCitizen.LocalTelemetry\StarCitizen.LocalTelemetry.csproj --diagnostics RS0016 RS0017 --severity info
```

Run it again if the build still reports RS0016 or RS0017. Removing or changing a
shipped member is a breaking change; mention it in `CHANGELOG.md`. On release, move
the `Unshipped` entries to `Shipped`.

## Game updates

After a game patch:

1. `.\tools\Update-Proto.ps1` refreshes the protobuf schema and `TelemetrySchema.BundledGameVersion`.
2. `.\tools\Update-Actions.ps1` refreshes `GameActions.cs` and the API files.
3. Map new fields in `TelemetryModels.cs` and `TelemetryMapper.cs`, update the
   documented game version in `README.md` and `docs\`, and add a `CHANGELOG.md` entry.

See [tools/README.md](tools/README.md).

## Versioning and releases

Versions come from git tags via MinVer; see [README.md](README.md#versioning) for
the policy. To release:

1. Rename `## [Unreleased]` in `CHANGELOG.md` to the new version and date.
2. Move the `PublicAPI.Unshipped.txt` entries to `PublicAPI.Shipped.txt`.
3. Commit, then tag and push: `git tag v0.1.0-preview.1` and `git push origin v0.1.0-preview.1`.

The release workflow runs CI, publishes the package to NuGet.org using
[Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing),
and creates a GitHub release with the packages attached; tags containing `-` are
marked as prereleases. Configure a trusted-publishing policy for `release.yml`
and add the GitHub Actions repository secret `NUGET_USER` with your NuGet.org
profile username. No long-lived API key is needed.
