# Actions

The game advertises the actions it accepts through `ListTriggerableActions`.
`GameActions` has a constant for each action seen in PTU `5.0.202.10025`, grouped
by category. Each category class also has a `Category` constant, and
`GameActions.Known` lists all of them as `GameAction` records.

> Triggering an action changes the game, as if the player pressed the key binding.
> Only trigger actions in response to an explicit user request.

## Categories

| Class | Game category | Actions |
|---|---|---|
| `LightsController` | `lights_controller` | 3 |
| `Player` | `player` | 4 |
| `PlayerEmotes` | `player_emotes` | 34 |
| `SeatGeneral` | `seat_general` | 17 |
| `SpaceshipDocking` | `spaceship_docking` | 1 |
| `SpaceshipGeneral` | `spaceship_general` | 11 |
| `SpaceshipMovement` | `spaceship_movement` | 27 |
| `SpaceshipPower` | `spaceship_power` | 12 |
| `SpaceshipQuantum` | `spaceship_quantum` | 1 |
| `SpaceshipRadar` | `spaceship_radar` | 1 |
| `SpaceshipTargetHailing` | `spaceship_target_hailing` | 1 |

Only `v_invoke_ping` (`SpaceshipRadar.InvokePing`) is holdable in this build.

Constant names come from the action names. The `v_` prefix is removed, as is
`emote_` in `player_emotes`. The rest is converted to PascalCase, for example
`v_lights_on` → `LightsOn`.

## Helpers

| Helper | Actions |
|---|---|
| `SetLightsAsync(on)` / `ToggleLightsAsync()` | `v_lights_on`, `v_lights_off` / `v_lights` |
| `SetLandingGearAsync(deployed)` | `v_deploy_landing_system`, `v_retract_landing_system` |
| `SetVtolAsync(on)` | `v_vtol_on`, `v_vtol_off` |
| `SetMasterModeAsync(mode)` | `v_master_mode_set_nav`, `v_master_mode_set_scm` |
| `SetDecoupledAsync(decoupled)` | `v_ifcs_vector_decoupling_on`, `_off` |
| `SetGSafeAsync(on)` | `v_ifcs_gsafe_on`, `_off` |
| `SetProximityAssistAsync(on)` | `v_ifcs_proximity_assist_on`, `_off` |
| `FlightReadyAsync()` | `v_flightready` |
| `SetPowerAsync(on)` | `v_power_set_on`, `_off` |
| `SetShieldPowerAsync(on)` / `SetWeaponPowerAsync(on)` / `SetThrusterPowerAsync(on)` | `v_power_set_shields_*`, `v_power_set_weapons_*`, `v_power_set_thrusters_*` |
| `SetDoorsOpenAsync(open)` / `SetDoorsLockedAsync(locked)` | `v_open_all_doors`, `v_close_all_doors` / `v_lock_all_doors`, `v_unlock_all_doors` |
| `SetLightAmplificationAsync(on)` | `v_light_amplification_on`, `_off` |
| `ToggleQuantumDriveAsync()` | `v_toggle_qdrive_engagement` |
| `RadarPingAsync(hold)` | `v_invoke_ping` |

`SetMasterModeAsync` throws `ArgumentOutOfRangeException` for any mode other than
`Navigation` or `Scm`.

## Results and retries

Every trigger returns an `ActionResult`. Check `IsSuccess`: the game can reject an
action, for example when the player isn't seated. A rejection is reported in
`Status` (and sometimes `Message`), not as an exception.

Triggers are never retried. If a call fails in transit, it's unknown whether the
game performed the action. Confirm through telemetry where possible, for example
`Vehicle.LightsOn` after `SetLightsAsync`.

`TriggerKnownActionAsync` first fetches the advertised actions. It throws
`ArgumentException` if the game doesn't advertise the action, or if a hold is
given for an action that isn't holdable.

## Updating the catalog

Other game builds may add or remove actions. Use `GetActionsAsync` to discover
them at runtime, and regenerate `GameActions.cs` with
[`tools\Update-Actions.ps1`](../tools/README.md#update-actionsps1). Don't edit
`GameActions.cs` by hand. A test checks that it matches the generator output.
