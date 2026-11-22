namespace StarCitizen.LocalTelemetry;

/// <summary>Known action pairs that have a corresponding telemetry state.</summary>
/// <remarks>The catalog reflects the bundled schema, not the actions advertised by a running game.</remarks>
public static class GameToggles
{
    /// <summary>Known toggle definitions, with stable IDs for application settings.</summary>
    public static ValueList<GameToggle> Known { get; } = new(
    [
        new("lights", "Lights", "Vehicle", GameActions.LightsController.LightsOn, GameActions.LightsController.LightsOff,
            (v, _) => v?.LightsOn),
        new("gear", "Landing Gear", "Flight", GameActions.SpaceshipMovement.DeployLandingSystem, GameActions.SpaceshipMovement.RetractLandingSystem,
            (v, _) => Deployment(v?.Flight?.LandingGear)),
        new("vtol", "VTOL", "Flight", GameActions.SpaceshipMovement.VtolOn, GameActions.SpaceshipMovement.VtolOff,
            (v, _) => Deployment(v?.Flight?.Vtol)),
        new("decoupled", "Decoupled", "Flight", GameActions.SpaceshipMovement.IfcsVectorDecouplingOn, GameActions.SpaceshipMovement.IfcsVectorDecouplingOff,
            (v, _) => v?.Flight?.Settings?.Coupled is { } coupled ? !coupled.Enabled : null),
        new("gsafe", "G-Safe", "Flight", GameActions.SpaceshipMovement.IfcsGsafeOn, GameActions.SpaceshipMovement.IfcsGsafeOff,
            (v, _) => v?.Flight?.Settings?.GSafe?.Enabled),
        new("proximity", "Proximity Assist", "Flight", GameActions.SpaceshipMovement.IfcsProximityAssistOn, GameActions.SpaceshipMovement.IfcsProximityAssistOff,
            (v, _) => v?.Flight?.Settings?.ProximityAssist?.Enabled),
        new("mastermode", "Navigation Mode", "Flight", GameActions.SpaceshipMovement.MasterModeSetNav, GameActions.SpaceshipMovement.MasterModeSetScm,
            (v, _) => v?.MasterMode?.Mode switch { MasterMode.Navigation => true, MasterMode.Scm => false, _ => null }),
        new("power", "Power", "Power", GameActions.SpaceshipPower.PowerSetOn, GameActions.SpaceshipPower.PowerSetOff,
            (v, _) => v?.Power?.Powerplants),
        new("shields", "Shield Power", "Power", GameActions.SpaceshipPower.PowerSetShieldsOn, GameActions.SpaceshipPower.PowerSetShieldsOff,
            (v, _) => v?.Power?.ShieldGenerators),
        new("weapons", "Weapon Power", "Power", GameActions.SpaceshipPower.PowerSetWeaponsOn, GameActions.SpaceshipPower.PowerSetWeaponsOff,
            (v, _) => v?.Power?.Weapons),
        new("engines", "Thruster Power", "Power", GameActions.SpaceshipPower.PowerSetThrustersOn, GameActions.SpaceshipPower.PowerSetThrustersOff,
            (v, _) => v?.Power?.Engines),
        new("doors", "Doors Open", "Vehicle", GameActions.SpaceshipGeneral.OpenAllDoors, GameActions.SpaceshipGeneral.CloseAllDoors,
            (v, _) => v?.Doors?.OpenState switch { DoorOpenState.Opened => true, DoorOpenState.Closed => false, _ => null }),
        new("locks", "Doors Locked", "Vehicle", GameActions.SpaceshipGeneral.LockAllDoors, GameActions.SpaceshipGeneral.UnlockAllDoors,
            (v, _) => v?.Doors?.LockState switch { DoorLockState.Locked => true, DoorLockState.Unlocked => false, _ => null }),
        new("nightvision", "Light Amplification", "Vehicle", GameActions.SeatGeneral.LightAmplificationOn, GameActions.SeatGeneral.LightAmplificationOff,
            (v, t) => v?.Flight?.Settings?.LightAmplification ?? t?.LightAmplification)
    ]);

    private static bool? Deployment(DeploymentState? state) => state switch
    {
        DeploymentState.Deployed => true,
        DeploymentState.Retracted => false,
        _ => null
    };
}
