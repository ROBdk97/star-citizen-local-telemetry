# Telemetry reference

Each message from the game is mapped to a `TelemetryFrame`. A frame contains only
the sections the game sent in that message; missing sections are `null`, and the
game often sends partial frames. Keep the last value of each section if you need
a full snapshot. The Telemetry Explorer sample does this.

The game doesn't document units, ranges or sentinel values. The descriptions
below are based on observations from PTU `5.0.202.10025`. Every type and member
also has XML documentation, so IntelliSense shows the same information.

## Reading the stream

| Method | Behaviour |
|---|---|
| `ReadTelemetryAsync` | One gRPC stream. Ends when the game ends the stream; errors propagate. |
| `ReadTelemetryWithReconnectAsync(retryDelay)` | Reconnects indefinitely after a stream end or gRPC `Unavailable`, waiting `retryDelay` (default 2 s, must be positive). Other errors propagate. Frames sent while disconnected are lost. |
| `ObserveTelemetryAsync(retryDelay)` | Like the reconnecting stream, but yields `TelemetryStreamEvent`s: `Connecting`, `Connected` (on the first frame), `Frame`, `SchemaMismatch` and `Disconnected` (with a `Reason`). Fatal errors propagate instead of being reported as a disconnect. |

Canceling the token throws `OperationCanceledException`, not `RpcException`, and
stops reading and retrying. Each enumeration opens its own gRPC stream. Dispose the
client when finished.

The `Vehicles()`, `Actors()`, `Weapons()` and `GameEvents()` extensions filter a
frame stream to one section. `ObserveChanges()` yields only frames whose sections
changed, compared by value (see [Equality](#equality)) against the last value of
that section during the enumeration:

```csharp
await foreach (var change in client.ReadTelemetryWithReconnectAsync(cancellationToken: ct).ObserveChanges(ct))
{
    if (change.Sections.HasFlag(TelemetrySections.Vehicle))
        Console.WriteLine(change.Frame.Vehicle!.Name);
}
```

A missing section isn't treated as a removal, and the result is the original frame,
not a merged snapshot. Nonempty event lists are always delivered, because events
may repeat.

## Frame

| Property | Type | Notes |
|---|---|---|
| `ReceivedAt` | `DateTimeOffset` | Local receive time; the game sends no timestamp. |
| `Vehicle` | `VehicleTelemetry?` | `null` when not in a vehicle, or not sent. |
| `Weapons` | `WeaponsTelemetry?` | The vehicle's weapons. |
| `Events` | `ValueList<GameEventType>` | Empty when there are none. Events can repeat. |
| `Actor` | `ActorTelemetry?` | The player character. |
| `ForceFeedback` | `ForceFeedbackTelemetry?` | Controller force feedback. |
| `Turret` | `TurretTelemetry?` | The turret the player operates. |

## Vehicle

`VehicleTelemetry`: `Name`, `ClassName`, `Manufacturer`, `Type` (`VehicleType`),
`LightsOn`, `Health`, and the nested records below.

- `MasterMode` → `MasterModeStatus(Mode, Spooled)`. `Mode` is `Navigation` or `Scm`.
- `Doors` → `DoorStatus(OpenState, LockState)`.
- `Shields` → `ShieldStatus(Level, Faces)`, where `Faces` lists per-face levels.
- `Power` → `PowerStatus`: one `bool` per group (`Powerplants`, `Engines`, `Weapons`,
  `Coolers`, `LifeSupport`, `GravityGenerators`, `Sensors`, `ShieldGenerators`, `ShieldEmitters`).
- `Annunciators` → one `AnnunciatorState` per cockpit warning light (`Power`, `Weapons`,
  `Thrusters`, `Shields`, `Coolers`, `QuantumDrive`, `Radar`, `Proximity`, `Hit`, `Missile`).
- `Flight` → `FlightTelemetry`:
  - `IsLanded`.
  - `LandingGear`, `Vtol`, `Transformation`: `DeploymentState?`. `null` means
    the game didn't report it, for example on a ship without VTOL.
  - `Settings` → `FlightSettings`. IFCS and HUD settings. Most are
    `Availability(Enabled, Active)`: `Enabled` is the switch, `Active` is whether
    it currently applies.
  - `Instruments` → `FlightInstruments`: `Speed`, `HydrogenFuel`, `QuantumFuel`,
    `GMeter` (`Gauge`), `Boost` (`BoostStatus`), `Altitude`, `RadarAltitude`,
    `VerticalSpeed`, `AtmosphericDensity`, `Heading`.

`Altitude` and `RadarAltitude` are `null` when the game sends `float.MaxValue`.
This was observed in space. Reading it as "unavailable" is an assumption.

## Weapons

`WeaponsTelemetry`: `PresetIndex`, `PresetName`, `Settings` (`WeaponSettings`:
pips, convergence, staggered fire, gimbal lock), `Weapons` (a list of
`Weapon(Name, IsPowered, ReadyToFire, InPreset)`) and `Countermeasures` (a list of
`Countermeasure(Type, Amount)`).

## Actor

`ActorTelemetry` has three parts:

- `Vitals` → `ActorVitals`: `Health`, `Oxygen`, `Hunger`, `Thirst`, `BodyTemperature`
  as `Gauge(Current, Max)`, plus `HeartRate`, `Gravity`, `Pressure`, `IntoxicationLevel`.
  `Gauge.Fraction` is `Current / Max`, or `null` when `Max` is 0.
- `State` → `ActorStatus(Locomotion, CrewRole, SeatName, UecBalance)`.
- `Environment` → `ActorEnvironment(ExternalTemperature, AmbientRadiationRate, BreathableAtmosphere)`.

## Force feedback and turret

- `ForceFeedbackTelemetry`: `Angular` and `Linear` are `FeedbackAxes(X, Y, Z)` of
  `AxisFeedback(Resistance, Damping)`. Also `Gunfire` and `StickShaker` intensity.
- `TurretTelemetry`: `LightAmplification`.

## Equality

All models are records. Lists use `ValueList<T>`, which compares its elements, so
two frames with the same content are equal. `ObserveChanges()` relies on this to
report only changed sections.

## Schema mismatches

The models are mapped from the schema bundled with the library (`TelemetrySchema.BundledGameVersion`).
When the game sends data that schema doesn't describe, the client reports a `SchemaMismatch`:

| Kind | Meaning |
|---|---|
| `UnknownField` | A field number the schema doesn't define. Its data isn't mapped. |
| `UnknownEnumValue` | An enum value the schema doesn't define. It's passed through as an undefined enum value. |
| `ChangedFieldType` | A known field sent with a different wire type. Its data isn't mapped. |

`Path` is the protobuf path of the containing message or field, for example
`TelemetryStreamResponse.vehicle.state.master_mode.mode`, and `Number` is the field number or enum value.
Each distinct mismatch is reported once per client: as a warning log, as a `SchemaMismatch` event from
`ObserveTelemetryAsync` (before the frame that contained it) and in `client.SchemaMismatches`.
Update the schema with [tools/Update-Proto.ps1](../tools/README.md) when mismatches appear.
