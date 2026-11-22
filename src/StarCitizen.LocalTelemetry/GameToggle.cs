namespace StarCitizen.LocalTelemetry;

/// <summary>A known pair of actions and the telemetry state they control.</summary>
/// <remarks>
/// These mappings are based on the bundled game schema and action catalog; the running game
/// may not advertise or support either action. A successful request does not guarantee that
/// the state has changed: confirm it with telemetry.
/// </remarks>
public sealed class GameToggle
{
    private readonly Func<VehicleTelemetry?, TurretTelemetry?, bool?> _readState;

    internal GameToggle(string id, string name, string category, string onAction, string offAction,
        Func<VehicleTelemetry?, TurretTelemetry?, bool?> readState)
    {
        Id = id;
        Name = name;
        Category = category;
        OnAction = onAction;
        OffAction = offAction;
        _readState = readState;
    }

    /// <summary>Stable identifier for saving a selection.</summary>
    public string Id { get; }
    /// <summary>Human-readable English name.</summary>
    public string Name { get; }
    /// <summary>Human-readable group for presentation.</summary>
    public string Category { get; }
    /// <summary>Action that requests the on state.</summary>
    public string OnAction { get; }
    /// <summary>Action that requests the off state.</summary>
    public string OffAction { get; }

    /// <summary>Reads the reported state from the latest telemetry sections.</summary>
    /// <param name="vehicle">The latest vehicle section, or <see langword="null"/> if unavailable.</param>
    /// <param name="turret">The latest turret section, or <see langword="null"/> if unavailable.</param>
    /// <returns>The state, or <see langword="null"/> if unavailable, unknown or transitioning.</returns>
    /// <remarks>Frames can be partial. Pass the latest sections and clear them on disconnect or when leaving the vehicle.</remarks>
    public bool? GetState(VehicleTelemetry? vehicle, TurretTelemetry? turret = null) => _readState(vehicle, turret);

    /// <summary>Requests a specific state, without retrying.</summary>
    /// <param name="client">The client used to send the request.</param>
    /// <param name="on">The desired state.</param>
    /// <param name="cancellationToken">Cancels the request; the game may still have performed the action.</param>
    /// <returns>The game's response; inspect <see cref="ActionResult.IsSuccess"/>.</returns>
    public Task<ActionResult> SetAsync(ITelemetryClient client, bool on, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.TriggerActionAsync(on ? OnAction : OffAction, cancellationToken: cancellationToken);
    }
}
