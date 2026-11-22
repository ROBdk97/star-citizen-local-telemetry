using Proto = Sc.Game.Telemetry.V1;

namespace StarCitizen.LocalTelemetry;

/// <summary>Converts the generated protobuf messages into the public C# models.</summary>
internal static class TelemetryMapper
{
    public static TelemetryFrame ToModel(this Proto.TelemetryStreamResponse frame, DateTimeOffset receivedAt) => new(
        receivedAt,
        frame.Vehicle?.State?.ToModel(),
        frame.Weapons?.ToModel(),
        frame.Events is null
            ? ValueList<GameEventType>.Empty
            : new(frame.Events.Events.Select(e => (GameEventType)(int)e.Type)),
        frame.Actor?.ToModel(),
        frame.ForceFeedback?.ToModel(),
        frame.Turret is null ? null : new TurretTelemetry(frame.Turret.LightAmplification));

    public static VehicleTelemetry ToModel(this Proto.VehicleState s) => new(
        s.Name,
        s.ClassName,
        s.Manufacturer,
        (VehicleType)(int)s.Type,
        s.LightsEnabled,
        s.Doors is { } d ? new((DoorOpenState)(int)d.OpenState, (DoorLockState)(int)d.LockState) : null,
        s.Annunciators?.ToModel(),
        s.Flight?.ToModel(),
        s.Power is { } p
            ? new(p.Powerplants, p.Engines, p.Weapons, p.Coolers, p.LifeSupport, p.GravityGenerators, p.Sensors,
                p.ShieldGenerators, p.ShieldEmitters)
            : null,
        s.Shields is { } sh ? new(sh.Level, new ValueList<float>(sh.Faces)) : null,
        s.Health,
        s.MasterMode is { } m ? new((MasterMode)(int)m.Mode, m.Spooled) : null);

    private static Annunciators ToModel(this Proto.VehicleAnnunciator a) => new(
        Map(a.Pwr), Map(a.Wpn), Map(a.Thr), Map(a.Shld), Map(a.Cool),
        Map(a.Qdrv), Map(a.Radr), Map(a.Prox), Map(a.Hit), Map(a.Misl));

    private static AnnunciatorState Map(Proto.VehicleAnnunciator.Types.AnnunciatorState state) =>
        (AnnunciatorState)(int)state;

    private static FlightTelemetry ToModel(this Proto.VehicleFlight f) => new(
        f.State?.IsLanded ?? false,
        f.State is { HasGear: true } ? (DeploymentState)(int)f.State.Gear : null,
        f.State is { HasVtol: true } ? (DeploymentState)(int)f.State.Vtol : null,
        f.State is { HasTransformation: true } ? (DeploymentState)(int)f.State.Transformation : null,
        f.Settings?.ToModel(),
        f.Instruments?.ToModel());

    private static FlightSettings ToModel(this Proto.VehicleFlightSettings s) => new(
        Map(s.IfcsCoupled),
        Map(s.IfcsEsp),
        Map(s.IfcsGravityCompensation),
        Map(s.IfcsGSafe),
        Map(s.IfcsCruiseMode),
        s.IfcsSpacebreakEnablesBoost,
        Map(s.IfcsSpeedLimiter),
        Map(s.IfcsProximityAssist),
        Map(s.IfcsProximityAssistLanding),
        Map(s.IfcsStability),
        Map(s.IfcsCore),
        s.AdvancedHudEnabledScm,
        s.AdvancedHudEnabledNav,
        s.AdvancedHudLabels,
        s.CoursePrediction,
        Map(s.IfcsWindCompensation),
        s.IfcsGSafeDisabledByBoost,
        Map(s.IfcsCommandBehaviour),
        s.FlightLightAmplification);

    private static Availability? Map(Proto.Availability? a) => a is null ? null : new(a.Enabled, a.Active);

    private static Gauge? Map(Proto.Gauge? g) => g is null ? null : new(g.Current, g.Max);

    private static FlightInstruments ToModel(this Proto.VehicleFlightInstruments i) => new(
        i.Speed,
        i.HydrogenFuel,
        i.QuantumFuel,
        Map(i.GMeter),
        i.Boost is { } b ? new(b.Engaged, b.Output, b.Reserves, b.Critical) : null,
        Available(i.Altitude),
        Available(i.RadarAltitude),
        i.VerticalSpeedIndicator,
        i.AtmosphericDensity,
        i.Heading);

    private static float? Available(float value) => value == float.MaxValue ? null : value;

    private static WeaponsTelemetry ToModel(this Proto.WeaponsFrame w) => new(
        w.Settings is { } s
            ? new(s.FallbackConvergenceDistance, s.MagnifiedUi, s.PrecisionLines, s.StaggeredFire, s.UseLagPips,
                s.UseSinglePips, s.UseFadingPips, s.GimbalsLocked)
            : null,
        w.PresetIndex,
        w.PresetName,
        new(w.Weapons.Select(x => new Weapon(x.Name, x.IsPowered, x.ReadyToFire, x.ContainedInPreset))),
        new(w.Countermeasures.Select(x => new Countermeasure((CountermeasureType)(int)x.Type, x.Amount))));

    private static ActorTelemetry ToModel(this Proto.ActorFrame a) => new(
        a.Vitals is { } v
            ? new(Map(v.Health), Map(v.Oxygen), Map(v.Hunger), Map(v.Thirst), Map(v.BodyTemperature),
                v.HeartRate, v.Gravity, v.Pressure, v.IntoxicationLevel)
            : null,
        a.State is { } s
            ? new((LocomotionState)(int)s.Locomotion, (CrewRole)(int)s.CrewRole, s.SeatName, s.UecBalance)
            : null,
        a.Environment is { } e
            ? new(e.ExternalTemperature, e.AmbientRadiationRate, e.BreathableAtmosphere)
            : null);

    private static ForceFeedbackTelemetry ToModel(this Proto.ForceFeedbackFrame f) => new(
        Map(f.Angular), Map(f.Linear), f.Gunfire, f.StickShaker);

    private static FeedbackAxes? Map(Proto.FeedbackAxes? a) => a is null ? null : new(Map(a.X), Map(a.Y), Map(a.Z));

    private static AxisFeedback? Map(Proto.AxisFeedback? a) => a is null ? null : new(a.Resistance, a.Damping);

    public static GameAction ToModel(this Proto.TriggerableAction a) => new(a.ActionName, a.Category, a.Holdable);

    public static ActionResult ToModel(this Proto.TriggerGameActionResponse r) =>
        new((ActionStatus)(int)r.Status, r.Message);
}
