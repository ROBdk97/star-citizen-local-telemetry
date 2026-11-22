# Tools

## Update-Proto.ps1

Updates `src\StarCitizen.LocalTelemetry\Protos\sc_telemetry_types.proto` after a game patch.

The schema comes from [sc_make_proto](https://github.com/Purzifal/sc_make_proto). It reads
the protobuf descriptors embedded in a running `StarCitizen.exe`. With the game running,
extract and apply the schema in one step:

```powershell
.\tools\Update-Proto.ps1
```

The first run needs Python 3 on `PATH` (or pass `-Python <path>`). It downloads
sc_make_proto at a pinned commit (`-MakeProtoRef`) into `.tools\sc_make_proto` (git-ignored;
change it with `-CachePath`) and creates a virtual environment there with a pinned
`protobuf` (`-ProtobufVersion`). Later runs reuse the cache. If the game isn't running,
make_proto.py waits for it to start.

To use your own sc_make_proto checkout and Python instead:

```powershell
.\tools\Update-Proto.ps1 -MakeProtoPath D:\SC-Telemetry\sc_make_proto -Python D:\SC-Telemetry\sc_make_proto\.venv\Scripts\python.exe
```

Or apply a schema you extracted earlier:

```powershell
.\tools\Update-Proto.ps1 -Source D:\SC-Telemetry\sc_telemetry_5.0.202.10025.proto
```

The script:

1. Checks that the file contains the `sc.game.telemetry.v1` package and the three RPCs.
2. Replaces the library's proto, updates `TelemetrySchema.BundledGameVersion` and shows a diff summary.
3. Rebuilds the library.

Use `-WhatIf` to preview without changing anything, and `-SkipBuild` to skip the build.

If the build fails, the schema changed in a way the public models don't handle yet.
Update `TelemetryModels.cs` and `TelemetryMapper.cs`, then run the tests. Also update
the game version in `README.md` and `docs\`, add a `CHANGELOG.md` entry, and run
`Update-Actions.ps1` to refresh the action catalog. New public members must be added to
`PublicAPI.Unshipped.txt`; see [CONTRIBUTING.md](../CONTRIBUTING.md).

## Update-Actions.ps1

Regenerates `src\StarCitizen.LocalTelemetry\GameActions.cs` from the actions the
running game advertises. It runs `tools\GameActionsGenerator`, which calls only
`ListTriggerableActions` and never triggers anything.

```powershell
# Check whether GameActions.cs matches the game; nonzero exit code if it doesn't
# or if the game can't be reached.
.\tools\Update-Actions.ps1 -Check

# Rewrite GameActions.cs.
.\tools\Update-Actions.ps1
```

| Parameter | Default | Purpose |
|---|---|---|
| `-Check` | off | Compare only. Lists added (`+`), removed (`-`) and changed (`~`) actions. |
| `-Address` | `http://127.0.0.1:2942` | The game's telemetry endpoint. |

The game version in the file header comes from the `file version` line of the
library's proto file, so run `Update-Proto.ps1` first after a patch. The game must
be running with telemetry enabled.

Constant names are derived from action names (see [docs/actions.md](../docs/actions.md)).
Colliding names get a numeric suffix. Removing or renaming an action is a breaking
change for library users.

After regenerating, run the tests. `GameActionsGeneratorTests` checks that the file
matches the generator output, so if you change `GameActionsWriter`, regenerate too.
