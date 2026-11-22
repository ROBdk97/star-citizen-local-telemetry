namespace StarCitizen.LocalTelemetry;

// Enum values mirror the game's protobuf numbering; 0 means the game did not specify a value.

/// <summary>Deployment state of a retractable vehicle component such as landing gear.</summary>
public enum DeploymentState
{
    /// <summary>The game did not report a state.</summary>
    Unknown,
    /// <summary>Fully retracted.</summary>
    Retracted,
    /// <summary>Moving towards <see cref="Deployed"/>.</summary>
    Deploying,
    /// <summary>Fully deployed.</summary>
    Deployed,
    /// <summary>Moving towards <see cref="Retracted"/>.</summary>
    Retracting
}

/// <summary>Kind of vehicle.</summary>
public enum VehicleType
{
    /// <summary>The game did not report a type.</summary>
    Unknown,
    /// <summary>A spaceship.</summary>
    Spaceship,
    /// <summary>A wheeled or tracked ground vehicle.</summary>
    Ground,
    /// <summary>A grav-lev vehicle (hover bike).</summary>
    Gravlev,
    /// <summary>A boat.</summary>
    Boat
}

/// <summary>Ship master mode.</summary>
public enum MasterMode
{
    /// <summary>The game did not report a mode.</summary>
    Unknown,
    /// <summary>Navigation (NAV) mode.</summary>
    Navigation,
    /// <summary>Standard combat maneuvering (SCM) mode.</summary>
    Scm,
    /// <summary>Stealth mode.</summary>
    Stealth
}

/// <summary>How the player character is moving.</summary>
public enum LocomotionState
{
    /// <summary>The game did not report a state.</summary>
    Unknown,
    /// <summary>Walking on foot.</summary>
    OnFoot,
    /// <summary>Extravehicular activity (floating in zero-g).</summary>
    Eva,
    /// <summary>Sitting in a seat.</summary>
    Seated
}

/// <summary>The player's role in the current vehicle.</summary>
public enum CrewRole
{
    /// <summary>The game did not report a role.</summary>
    Unknown,
    /// <summary>Pilot.</summary>
    Pilot,
    /// <summary>Gunner.</summary>
    Gunner,
    /// <summary>Other crew.</summary>
    Crew
}

/// <summary>Whether a vehicle's doors are open.</summary>
public enum DoorOpenState
{
    /// <summary>The game did not report a state.</summary>
    Unknown,
    /// <summary>Open.</summary>
    Opened,
    /// <summary>Closed.</summary>
    Closed
}

/// <summary>Whether a vehicle's doors are locked.</summary>
public enum DoorLockState
{
    /// <summary>The game did not report a state.</summary>
    Unknown,
    /// <summary>Unlocked.</summary>
    Unlocked,
    /// <summary>Locked.</summary>
    Locked
}

/// <summary>State of a cockpit warning light (annunciator).</summary>
public enum AnnunciatorState
{
    /// <summary>The game did not report a state.</summary>
    Unknown,
    /// <summary>Working normally.</summary>
    Nominal,
    /// <summary>Switched off.</summary>
    Off,
    /// <summary>Minor warning, flashing.</summary>
    MinorFlashing,
    /// <summary>Minor warning, steady.</summary>
    MinorOn,
    /// <summary>Major warning, flashing.</summary>
    MajorFlashing,
    /// <summary>Major warning, steady.</summary>
    MajorOn
}

/// <summary>Kind of countermeasure.</summary>
public enum CountermeasureType
{
    /// <summary>The game did not report a type.</summary>
    Unknown,
    /// <summary>Decoy flares.</summary>
    Decoy,
    /// <summary>Noise (chaff).</summary>
    Noise
}

/// <summary>A one-off game event reported in <see cref="TelemetryFrame.Events"/>.</summary>
public enum GameEventType
{
    /// <summary>The game did not report a type.</summary>
    Unknown,
    /// <summary>The player character spawned.</summary>
    ActorSpawn,
    /// <summary>The player character died.</summary>
    ActorDead,
    /// <summary>The player sat down in a seat.</summary>
    ActorEnterSeat,
    /// <summary>The player left a seat.</summary>
    ActorLeaveSeat,
    /// <summary>The player character respawned.</summary>
    ActorRespawn,
    /// <summary>The player character was downed (incapacitated).</summary>
    ActorDowned
}

/// <summary>The game's answer to a trigger request.</summary>
public enum ActionStatus
{
    /// <summary>The game did not report a status.</summary>
    Unknown,
    /// <summary>The game accepted the action.</summary>
    Ok,
    /// <summary>The game does not know the action name.</summary>
    UnknownAction,
    /// <summary>The action exists, but its input map isn't active right now (for example, a ship action while on foot).</summary>
    MapInactive,
    /// <summary>Too many requests; try again later.</summary>
    RateLimited,
    /// <summary>The game could not perform the action.</summary>
    Failed,
    /// <summary>A hold duration was sent for an action that can't be held.</summary>
    HoldNotAllowed,
    /// <summary>The game blocked the action.</summary>
    Filtered
}

/// <summary>One telemetry message. Sections the game did not include in this message are <see langword="null"/>.</summary>
/// <param name="ReceivedAt">When this client received the message (local clock).</param>
/// <param name="Vehicle">State of the vehicle the player is in.</param>
/// <param name="Weapons">Weapons of the current vehicle.</param>
/// <param name="Events">Game events reported in this message; empty when there were none. Events can repeat.</param>
/// <param name="Actor">State of the player character.</param>
/// <param name="ForceFeedback">Force-feedback values for controllers.</param>
/// <param name="Turret">State of the turret the player is operating.</param>
public sealed record TelemetryFrame(
    DateTimeOffset ReceivedAt,
    VehicleTelemetry? Vehicle,
    WeaponsTelemetry? Weapons,
    ValueList<GameEventType> Events,
    ActorTelemetry? Actor,
    ForceFeedbackTelemetry? ForceFeedback,
    TurretTelemetry? Turret);

/// <summary>A setting that can be switched on and that may or may not currently be in effect.</summary>
/// <param name="Enabled">Whether the setting is switched on.</param>
/// <param name="Active">Whether the setting is currently in effect.</param>
public sealed record Availability(bool Enabled, bool Active);

/// <summary>A value with a maximum, such as health or oxygen.</summary>
/// <param name="Current">The current value.</param>
/// <param name="Max">The maximum value.</param>
public sealed record Gauge(float Current, float Max)
{
    /// <summary><see cref="Current"/> divided by <see cref="Max"/>, or <see langword="null"/> when the maximum is zero.</summary>
    public float? Fraction => Max == 0 ? null : Current / Max;
}

/// <summary>State of the vehicle the player is in.</summary>
/// <param name="Name">Display name, for example <c>Arrow</c>.</param>
/// <param name="ClassName">The game's internal class name.</param>
/// <param name="Manufacturer">Manufacturer name.</param>
/// <param name="Type">Kind of vehicle.</param>
/// <param name="LightsOn">Whether the exterior lights are on.</param>
/// <param name="Doors">Combined door state.</param>
/// <param name="Annunciators">Cockpit warning lights.</param>
/// <param name="Flight">Flight state, settings and instruments.</param>
/// <param name="Power">Which component groups are powered.</param>
/// <param name="Shields">Shield levels.</param>
/// <param name="Health">Vehicle health as reported by the game; the scale is not documented.</param>
/// <param name="MasterMode">Master mode state.</param>
public sealed record VehicleTelemetry(
    string Name,
    string ClassName,
    string Manufacturer,
    VehicleType Type,
    bool LightsOn,
    DoorStatus? Doors,
    Annunciators? Annunciators,
    FlightTelemetry? Flight,
    PowerStatus? Power,
    ShieldStatus? Shields,
    float Health,
    MasterModeStatus? MasterMode);

/// <summary>Combined state of a vehicle's doors.</summary>
/// <param name="OpenState">Whether the doors are open.</param>
/// <param name="LockState">Whether the doors are locked.</param>
public sealed record DoorStatus(DoorOpenState OpenState, DoorLockState LockState);

/// <summary>Master mode state.</summary>
/// <param name="Mode">The current master mode.</param>
/// <param name="Spooled">Whether the mode has finished spooling up.</param>
public sealed record MasterModeStatus(MasterMode Mode, bool Spooled);

/// <summary>Shield levels.</summary>
/// <param name="Level">Overall shield level as reported by the game; the scale is not documented.</param>
/// <param name="Faces">Per-face shield levels, in the order the game reports them.</param>
public sealed record ShieldStatus(float Level, ValueList<float> Faces);

/// <summary>Which component groups of the vehicle are powered.</summary>
/// <param name="Powerplants">Power plants.</param>
/// <param name="Engines">Engines and thrusters.</param>
/// <param name="Weapons">Weapons.</param>
/// <param name="Coolers">Coolers.</param>
/// <param name="LifeSupport">Life support.</param>
/// <param name="GravityGenerators">Gravity generators.</param>
/// <param name="Sensors">Sensors and radar.</param>
/// <param name="ShieldGenerators">Shield generators.</param>
/// <param name="ShieldEmitters">Shield emitters.</param>
public sealed record PowerStatus(
    bool Powerplants,
    bool Engines,
    bool Weapons,
    bool Coolers,
    bool LifeSupport,
    bool GravityGenerators,
    bool Sensors,
    bool ShieldGenerators,
    bool ShieldEmitters);

/// <summary>Cockpit warning lights (annunciators).</summary>
/// <param name="Power">Power (PWR).</param>
/// <param name="Weapons">Weapons (WPN).</param>
/// <param name="Thrusters">Thrusters (THR).</param>
/// <param name="Shields">Shields (SHLD).</param>
/// <param name="Coolers">Coolers (COOL).</param>
/// <param name="QuantumDrive">Quantum drive (QDRV).</param>
/// <param name="Radar">Radar (RADR).</param>
/// <param name="Proximity">Proximity warning (PROX).</param>
/// <param name="Hit">Hit warning (HIT).</param>
/// <param name="Missile">Missile warning (MISL).</param>
public sealed record Annunciators(
    AnnunciatorState Power,
    AnnunciatorState Weapons,
    AnnunciatorState Thrusters,
    AnnunciatorState Shields,
    AnnunciatorState Coolers,
    AnnunciatorState QuantumDrive,
    AnnunciatorState Radar,
    AnnunciatorState Proximity,
    AnnunciatorState Hit,
    AnnunciatorState Missile);

/// <summary>Flight state, settings and instruments.</summary>
/// <param name="IsLanded">Whether the vehicle is landed.</param>
/// <param name="LandingGear">Landing gear state, or <see langword="null"/> when the game didn't report one (for example, no landing gear).</param>
/// <param name="Vtol">VTOL state, or <see langword="null"/> when not reported.</param>
/// <param name="Transformation">Transformation state (for vehicles that change shape), or <see langword="null"/> when not reported.</param>
/// <param name="Settings">Flight control settings.</param>
/// <param name="Instruments">Flight instruments.</param>
public sealed record FlightTelemetry(
    bool IsLanded,
    DeploymentState? LandingGear,
    DeploymentState? Vtol,
    DeploymentState? Transformation,
    FlightSettings? Settings,
    FlightInstruments? Instruments);

/// <summary>Flight control (IFCS) and HUD settings.</summary>
/// <param name="Coupled">Coupled flight mode.</param>
/// <param name="Esp">Enhanced stick precision (ESP).</param>
/// <param name="GravityCompensation">Gravity compensation.</param>
/// <param name="GSafe">G-force safety (G-Safe).</param>
/// <param name="CruiseMode">Cruise mode.</param>
/// <param name="SpacebrakeEnablesBoost">Whether the space brake also enables boost.</param>
/// <param name="SpeedLimiter">Speed limiter.</param>
/// <param name="ProximityAssist">Proximity assist.</param>
/// <param name="ProximityAssistLanding">Proximity assist while landing.</param>
/// <param name="Stability">Stability assist.</param>
/// <param name="Core">Core flight control assist, as reported by the game.</param>
/// <param name="AdvancedHudScm">Advanced HUD in SCM mode.</param>
/// <param name="AdvancedHudNav">Advanced HUD in NAV mode.</param>
/// <param name="AdvancedHudLabels">Advanced HUD labels.</param>
/// <param name="CoursePrediction">Course prediction display.</param>
/// <param name="WindCompensation">Wind compensation.</param>
/// <param name="GSafeDisabledByBoost">Whether boosting has disabled G-Safe.</param>
/// <param name="CommandBehaviour">Command behaviour setting, as reported by the game.</param>
/// <param name="LightAmplification">Whether light amplification (night vision) is on.</param>
public sealed record FlightSettings(
    Availability? Coupled,
    Availability? Esp,
    Availability? GravityCompensation,
    Availability? GSafe,
    Availability? CruiseMode,
    bool SpacebrakeEnablesBoost,
    Availability? SpeedLimiter,
    Availability? ProximityAssist,
    Availability? ProximityAssistLanding,
    Availability? Stability,
    Availability? Core,
    bool AdvancedHudScm,
    bool AdvancedHudNav,
    bool AdvancedHudLabels,
    bool CoursePrediction,
    Availability? WindCompensation,
    bool GSafeDisabledByBoost,
    Availability? CommandBehaviour,
    bool LightAmplification);

/// <summary>Flight instruments. Units are not documented by the game.</summary>
/// <param name="Speed">Speed.</param>
/// <param name="HydrogenFuel">Hydrogen fuel level.</param>
/// <param name="QuantumFuel">Quantum fuel level.</param>
/// <param name="GMeter">Current and maximum G-force.</param>
/// <param name="Boost">Boost state.</param>
/// <param name="Altitude">
/// <see langword="null"/> when the game reports <see cref="float.MaxValue"/>, which was observed in space
/// and is assumed to mean "unavailable".
/// </param>
/// <param name="RadarAltitude">As <paramref name="Altitude"/>, <see langword="null"/> for <see cref="float.MaxValue"/>.</param>
/// <param name="VerticalSpeed">Vertical speed.</param>
/// <param name="AtmosphericDensity">Atmospheric density.</param>
/// <param name="Heading">Heading.</param>
public sealed record FlightInstruments(
    float Speed,
    float HydrogenFuel,
    float QuantumFuel,
    Gauge? GMeter,
    BoostStatus? Boost,
    float? Altitude,
    float? RadarAltitude,
    float VerticalSpeed,
    float AtmosphericDensity,
    float Heading);

/// <summary>Boost (afterburner) state.</summary>
/// <param name="Engaged">Whether boost is engaged.</param>
/// <param name="Output">Current boost output.</param>
/// <param name="Reserves">Remaining boost reserves.</param>
/// <param name="Critical">Whether reserves are critically low.</param>
public sealed record BoostStatus(bool Engaged, float Output, float Reserves, bool Critical);

/// <summary>Weapons of the current vehicle.</summary>
/// <param name="Settings">Weapon and targeting settings.</param>
/// <param name="PresetIndex">Index of the active weapon preset.</param>
/// <param name="PresetName">Name of the active weapon preset.</param>
/// <param name="Weapons">The vehicle's weapons.</param>
/// <param name="Countermeasures">Countermeasure ammunition.</param>
public sealed record WeaponsTelemetry(
    WeaponSettings? Settings,
    uint PresetIndex,
    string PresetName,
    ValueList<Weapon> Weapons,
    ValueList<Countermeasure> Countermeasures);

/// <summary>Weapon and targeting settings.</summary>
/// <param name="FallbackConvergenceDistance">Fallback convergence distance setting.</param>
/// <param name="MagnifiedUi">Magnified targeting UI.</param>
/// <param name="PrecisionLines">Precision targeting lines.</param>
/// <param name="StaggeredFire">Staggered fire.</param>
/// <param name="UseLagPips">Lag pips.</param>
/// <param name="UseSinglePips">Single pips.</param>
/// <param name="UseFadingPips">Fading pips.</param>
/// <param name="GimbalsLocked">Whether gimbals are locked.</param>
public sealed record WeaponSettings(
    bool FallbackConvergenceDistance,
    bool MagnifiedUi,
    bool PrecisionLines,
    bool StaggeredFire,
    bool UseLagPips,
    bool UseSinglePips,
    bool UseFadingPips,
    bool GimbalsLocked);

/// <summary>One weapon of the vehicle.</summary>
/// <param name="Name">Weapon name.</param>
/// <param name="IsPowered">Whether the weapon is powered.</param>
/// <param name="ReadyToFire">Whether the weapon can fire right now.</param>
/// <param name="InPreset">Whether the weapon is part of the active preset.</param>
public sealed record Weapon(string Name, bool IsPowered, bool ReadyToFire, bool InPreset);

/// <summary>Countermeasure ammunition.</summary>
/// <param name="Type">Kind of countermeasure.</param>
/// <param name="Amount">Remaining amount.</param>
public sealed record Countermeasure(CountermeasureType Type, uint Amount);

/// <summary>State of the player character.</summary>
/// <param name="Vitals">Health and body values.</param>
/// <param name="State">Movement, role and seat.</param>
/// <param name="Environment">Surroundings of the character.</param>
public sealed record ActorTelemetry(ActorVitals? Vitals, ActorStatus? State, ActorEnvironment? Environment);

/// <summary>Health and body values of the player character. Units are not documented by the game.</summary>
/// <param name="Health">Health.</param>
/// <param name="Oxygen">Oxygen supply.</param>
/// <param name="Hunger">Hunger.</param>
/// <param name="Thirst">Thirst.</param>
/// <param name="BodyTemperature">Body temperature.</param>
/// <param name="HeartRate">Heart rate.</param>
/// <param name="Gravity">Gravity experienced by the character.</param>
/// <param name="Pressure">Ambient pressure.</param>
/// <param name="IntoxicationLevel">Intoxication level.</param>
public sealed record ActorVitals(
    Gauge? Health,
    Gauge? Oxygen,
    Gauge? Hunger,
    Gauge? Thirst,
    Gauge? BodyTemperature,
    float HeartRate,
    float Gravity,
    float Pressure,
    int IntoxicationLevel);

/// <summary>Movement, role and seat of the player character.</summary>
/// <param name="Locomotion">How the character is moving.</param>
/// <param name="CrewRole">Role in the current vehicle.</param>
/// <param name="SeatName">Name of the occupied seat; empty when not seated.</param>
/// <param name="UecBalance">aUEC balance.</param>
public sealed record ActorStatus(LocomotionState Locomotion, CrewRole CrewRole, string SeatName, long UecBalance);

/// <summary>Surroundings of the player character.</summary>
/// <param name="ExternalTemperature">Outside temperature.</param>
/// <param name="AmbientRadiationRate">Ambient radiation rate.</param>
/// <param name="BreathableAtmosphere">Whether the atmosphere is breathable.</param>
public sealed record ActorEnvironment(float ExternalTemperature, float AmbientRadiationRate, bool BreathableAtmosphere);

/// <summary>Force-feedback values for controllers. Ranges are not documented by the game.</summary>
/// <param name="Angular">Rotational feedback per axis.</param>
/// <param name="Linear">Linear feedback per axis.</param>
/// <param name="Gunfire">Gunfire vibration intensity.</param>
/// <param name="StickShaker">Stick shaker intensity.</param>
public sealed record ForceFeedbackTelemetry(FeedbackAxes? Angular, FeedbackAxes? Linear, float Gunfire, float StickShaker);

/// <summary>Feedback for each axis.</summary>
/// <param name="X">X axis.</param>
/// <param name="Y">Y axis.</param>
/// <param name="Z">Z axis.</param>
public sealed record FeedbackAxes(AxisFeedback? X, AxisFeedback? Y, AxisFeedback? Z);

/// <summary>Feedback for one axis.</summary>
/// <param name="Resistance">Resistance (spring force).</param>
/// <param name="Damping">Damping.</param>
public sealed record AxisFeedback(float Resistance, float Damping);

/// <summary>State of the turret the player is operating.</summary>
/// <param name="LightAmplification">Whether light amplification (night vision) is on.</param>
public sealed record TurretTelemetry(bool LightAmplification);

/// <summary>An action the game can trigger, as reported by action discovery.</summary>
/// <param name="Name">The action name to pass to <see cref="StarCitizenTelemetryClient.TriggerActionAsync"/>.</param>
/// <param name="Category">The category the game groups the action under.</param>
/// <param name="Holdable">Whether the action accepts a hold duration.</param>
public sealed record GameAction(string Name, string Category, bool Holdable);

/// <summary>The game's response to a trigger request.</summary>
/// <param name="Status">The outcome.</param>
/// <param name="Message">An optional explanation from the game; often empty.</param>
public sealed record ActionResult(ActionStatus Status, string Message)
{
    /// <summary>Whether <see cref="Status"/> is <see cref="ActionStatus.Ok"/>.</summary>
    public bool IsSuccess => Status == ActionStatus.Ok;
}
