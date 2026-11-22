namespace StarCitizen.LocalTelemetry;

/// <summary>
/// Convenience wrappers for common <see cref="GameActions"/>. Each sends one action without retrying;
/// check <see cref="ActionResult.Status"/> because the game may reject it, for example when not seated.
/// </summary>
public static class GameActionExtensions
{
    /// <summary>Turns the vehicle's exterior lights on (<c>v_lights_on</c>) or off (<c>v_lights_off</c>).</summary>
    /// <param name="client">The client to send the action with.</param>
    /// <param name="on"><see langword="true"/> to turn the lights on.</param>
    /// <param name="cancellationToken">Cancels the request; the game may still have performed the action.</param>
    /// <returns>The game's response.</returns>
    public static Task<ActionResult> SetLightsAsync(this ITelemetryClient client, bool on,
        CancellationToken cancellationToken = default) =>
        Send(client, on ? GameActions.LightsController.LightsOn : GameActions.LightsController.LightsOff,
            cancellationToken);

    /// <summary>Toggles the vehicle's exterior lights (<c>v_lights</c>).</summary>
    /// <inheritdoc cref="SetLightsAsync" path="/param[@name='client']|/param[@name='cancellationToken']|/returns"/>
    public static Task<ActionResult> ToggleLightsAsync(this ITelemetryClient client,
        CancellationToken cancellationToken = default) =>
        Send(client, GameActions.LightsController.Lights, cancellationToken);

    /// <summary>Deploys (<c>v_deploy_landing_system</c>) or retracts (<c>v_retract_landing_system</c>) the landing gear.</summary>
    /// <param name="client">The client to send the action with.</param>
    /// <param name="deployed"><see langword="true"/> to deploy the landing gear.</param>
    /// <param name="cancellationToken">Cancels the request; the game may still have performed the action.</param>
    /// <returns>The game's response.</returns>
    public static Task<ActionResult> SetLandingGearAsync(this ITelemetryClient client, bool deployed,
        CancellationToken cancellationToken = default) =>
        Send(client, deployed
            ? GameActions.SpaceshipMovement.DeployLandingSystem
            : GameActions.SpaceshipMovement.RetractLandingSystem, cancellationToken);

    /// <summary>Turns VTOL mode on (<c>v_vtol_on</c>) or off (<c>v_vtol_off</c>).</summary>
    /// <inheritdoc cref="SetLightsAsync"/>
    public static Task<ActionResult> SetVtolAsync(this ITelemetryClient client, bool on,
        CancellationToken cancellationToken = default) =>
        Send(client, on ? GameActions.SpaceshipMovement.VtolOn : GameActions.SpaceshipMovement.VtolOff,
            cancellationToken);

    /// <exception cref="ArgumentOutOfRangeException">The mode has no direct action (only NAV and SCM do).</exception>
    /// <summary>Switches the master mode to NAV (<c>v_master_mode_set_nav</c>) or SCM (<c>v_master_mode_set_scm</c>).</summary>
    /// <param name="client">The client to send the action with.</param>
    /// <param name="mode"><see cref="MasterMode.Navigation"/> or <see cref="MasterMode.Scm"/>.</param>
    /// <param name="cancellationToken">Cancels the request; the game may still have performed the action.</param>
    /// <returns>The game's response.</returns>
    public static Task<ActionResult> SetMasterModeAsync(this ITelemetryClient client, MasterMode mode,
        CancellationToken cancellationToken = default) =>
        Send(client, mode switch
        {
            MasterMode.Navigation => GameActions.SpaceshipMovement.MasterModeSetNav,
            MasterMode.Scm => GameActions.SpaceshipMovement.MasterModeSetScm,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        }, cancellationToken);

    /// <summary>Switches decoupled flight mode on (<c>v_ifcs_vector_decoupling_on</c>) or off (<c>v_ifcs_vector_decoupling_off</c>).</summary>
    /// <param name="client">The client to send the action with.</param>
    /// <param name="decoupled"><see langword="true"/> for decoupled mode.</param>
    /// <param name="cancellationToken">Cancels the request; the game may still have performed the action.</param>
    /// <returns>The game's response.</returns>
    public static Task<ActionResult> SetDecoupledAsync(this ITelemetryClient client, bool decoupled,
        CancellationToken cancellationToken = default) =>
        Send(client, decoupled
            ? GameActions.SpaceshipMovement.IfcsVectorDecouplingOn
            : GameActions.SpaceshipMovement.IfcsVectorDecouplingOff, cancellationToken);

    /// <summary>Turns G-Safe on (<c>v_ifcs_gsafe_on</c>) or off (<c>v_ifcs_gsafe_off</c>).</summary>
    /// <inheritdoc cref="SetLightsAsync"/>
    public static Task<ActionResult> SetGSafeAsync(this ITelemetryClient client, bool on,
        CancellationToken cancellationToken = default) =>
        Send(client, on ? GameActions.SpaceshipMovement.IfcsGsafeOn : GameActions.SpaceshipMovement.IfcsGsafeOff,
            cancellationToken);

    /// <summary>Turns proximity assist on (<c>v_ifcs_proximity_assist_on</c>) or off (<c>v_ifcs_proximity_assist_off</c>).</summary>
    /// <inheritdoc cref="SetLightsAsync"/>
    public static Task<ActionResult> SetProximityAssistAsync(this ITelemetryClient client, bool on,
        CancellationToken cancellationToken = default) =>
        Send(client, on
            ? GameActions.SpaceshipMovement.IfcsProximityAssistOn
            : GameActions.SpaceshipMovement.IfcsProximityAssistOff, cancellationToken);

    /// <summary>Makes the ship flight ready (<c>v_flightready</c>).</summary>
    /// <inheritdoc cref="SetLightsAsync" path="/param[@name='client']|/param[@name='cancellationToken']|/returns"/>
    public static Task<ActionResult> FlightReadyAsync(this ITelemetryClient client,
        CancellationToken cancellationToken = default) =>
        Send(client, GameActions.SpaceshipGeneral.FlightReady, cancellationToken);

    /// <summary>Turns the ship's power on (<c>v_power_set_on</c>) or off (<c>v_power_set_off</c>).</summary>
    /// <inheritdoc cref="SetLightsAsync"/>
    public static Task<ActionResult> SetPowerAsync(this ITelemetryClient client, bool on,
        CancellationToken cancellationToken = default) =>
        Send(client, on ? GameActions.SpaceshipPower.PowerSetOn : GameActions.SpaceshipPower.PowerSetOff,
            cancellationToken);

    /// <summary>Powers the shields on (<c>v_power_set_shields_on</c>) or off (<c>v_power_set_shields_off</c>).</summary>
    /// <inheritdoc cref="SetLightsAsync"/>
    public static Task<ActionResult> SetShieldPowerAsync(this ITelemetryClient client, bool on,
        CancellationToken cancellationToken = default) =>
        Send(client, on ? GameActions.SpaceshipPower.PowerSetShieldsOn : GameActions.SpaceshipPower.PowerSetShieldsOff,
            cancellationToken);

    /// <summary>Powers the weapons on (<c>v_power_set_weapons_on</c>) or off (<c>v_power_set_weapons_off</c>).</summary>
    /// <inheritdoc cref="SetLightsAsync"/>
    public static Task<ActionResult> SetWeaponPowerAsync(this ITelemetryClient client, bool on,
        CancellationToken cancellationToken = default) =>
        Send(client, on ? GameActions.SpaceshipPower.PowerSetWeaponsOn : GameActions.SpaceshipPower.PowerSetWeaponsOff,
            cancellationToken);

    /// <summary>Powers the thrusters on (<c>v_power_set_thrusters_on</c>) or off (<c>v_power_set_thrusters_off</c>).</summary>
    /// <inheritdoc cref="SetLightsAsync"/>
    public static Task<ActionResult> SetThrusterPowerAsync(this ITelemetryClient client, bool on,
        CancellationToken cancellationToken = default) =>
        Send(client, on
            ? GameActions.SpaceshipPower.PowerSetThrustersOn
            : GameActions.SpaceshipPower.PowerSetThrustersOff, cancellationToken);

    /// <summary>Opens (<c>v_open_all_doors</c>) or closes (<c>v_close_all_doors</c>) all doors.</summary>
    /// <param name="client">The client to send the action with.</param>
    /// <param name="open"><see langword="true"/> to open the doors.</param>
    /// <param name="cancellationToken">Cancels the request; the game may still have performed the action.</param>
    /// <returns>The game's response.</returns>
    public static Task<ActionResult> SetDoorsOpenAsync(this ITelemetryClient client, bool open,
        CancellationToken cancellationToken = default) =>
        Send(client, open ? GameActions.SpaceshipGeneral.OpenAllDoors : GameActions.SpaceshipGeneral.CloseAllDoors,
            cancellationToken);

    /// <summary>Locks (<c>v_lock_all_doors</c>) or unlocks (<c>v_unlock_all_doors</c>) all doors.</summary>
    /// <param name="client">The client to send the action with.</param>
    /// <param name="locked"><see langword="true"/> to lock the doors.</param>
    /// <param name="cancellationToken">Cancels the request; the game may still have performed the action.</param>
    /// <returns>The game's response.</returns>
    public static Task<ActionResult> SetDoorsLockedAsync(this ITelemetryClient client, bool locked,
        CancellationToken cancellationToken = default) =>
        Send(client, locked ? GameActions.SpaceshipGeneral.LockAllDoors : GameActions.SpaceshipGeneral.UnlockAllDoors,
            cancellationToken);

    /// <summary>Turns light amplification (night vision) on (<c>v_light_amplification_on</c>) or off (<c>v_light_amplification_off</c>).</summary>
    /// <inheritdoc cref="SetLightsAsync"/>
    public static Task<ActionResult> SetLightAmplificationAsync(this ITelemetryClient client, bool on,
        CancellationToken cancellationToken = default) =>
        Send(client, on
            ? GameActions.SeatGeneral.LightAmplificationOn
            : GameActions.SeatGeneral.LightAmplificationOff, cancellationToken);

    /// <summary>Engages or disengages the quantum drive (<c>v_toggle_qdrive_engagement</c>).</summary>
    /// <inheritdoc cref="SetLightsAsync" path="/param[@name='client']|/param[@name='cancellationToken']|/returns"/>
    public static Task<ActionResult> ToggleQuantumDriveAsync(this ITelemetryClient client,
        CancellationToken cancellationToken = default) =>
        Send(client, GameActions.SpaceshipQuantum.ToggleQdriveEngagement, cancellationToken);

    /// <summary>Sends a radar ping (<c>v_invoke_ping</c>).</summary>
    /// <param name="client">The client to send the action with.</param>
    /// <param name="hold">Optional hold duration; the game reports the radar ping as holdable.</param>
    /// <param name="cancellationToken">Cancels the request; the game may still have performed the action.</param>
    /// <returns>The game's response.</returns>
    public static Task<ActionResult> RadarPingAsync(this ITelemetryClient client, TimeSpan? hold = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.TriggerActionAsync(GameActions.SpaceshipRadar.InvokePing, hold, cancellationToken);
    }

    private static Task<ActionResult> Send(ITelemetryClient client, string action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.TriggerActionAsync(action, cancellationToken: cancellationToken);
    }
}
