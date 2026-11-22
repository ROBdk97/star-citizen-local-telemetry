using StarCitizen.LocalTelemetry;
using TelemetryExplorer;

namespace StarCitizen.LocalTelemetry.Tests;

public class TelemetryExplorerTests
{
    private static TelemetryFrame Frame(VehicleTelemetry? vehicle = null, ActorTelemetry? actor = null, params GameEventType[] events) =>
        new(DateTimeOffset.UnixEpoch, vehicle, null, new ValueList<GameEventType>(events), actor, null, null);

    private static VehicleTelemetry Vehicle(string name, bool lights) =>
        new(name, "", "", VehicleType.Unknown, lights, null, null, null, null, null, 1, null);

    [Fact]
    public void ModelKeepsSectionsFromPartialFramesAndTracksChanges()
    {
        var time = new ManualTime();
        var model = new TelemetryModel(time);
        model.Apply(new TelemetryStreamEvent(TelemetryStreamEventKind.Frame, Frame(Vehicle("Arrow", false))));
        var firstChange = time.Now;
        time.Now += TimeSpan.FromSeconds(5);
        model.Apply(new TelemetryStreamEvent(TelemetryStreamEventKind.Frame,
            Frame(actor: new ActorTelemetry(null, null, null), events: GameEventType.ActorEnterSeat)));
        model.Apply(new TelemetryStreamEvent(TelemetryStreamEventKind.Frame, Frame(Vehicle("Arrow", true))));

        var snapshot = model.GetSnapshot();
        Assert.Equal(3, snapshot.FrameCount);
        Assert.Equal(["Actor", "Vehicle"], snapshot.Sections);
        var name = Assert.Single(snapshot.Fields, f => f.Path == "Vehicle.Name");
        Assert.Equal(("Arrow", "string", firstChange), (name.Value, name.Type, name.LastChanged));
        var lights = Assert.Single(snapshot.Fields, f => f.Path == "Vehicle.LightsOn");
        Assert.Equal(("true", time.Now), (lights.Value, lights.LastChanged));
        Assert.Contains(snapshot.Fields, f => f.Path == "Actor.Vitals" && f.Value == "—");
        Assert.Equal("ActorEnterSeat", Assert.Single(snapshot.Log).Text);
        Assert.Contains("\"LightsOn\": true", snapshot.Json);
    }

    [Fact]
    public void ModelFlattensListsCapsLogAndReportsConnectionState()
    {
        var model = new TelemetryModel();
        var weapons = new WeaponsTelemetry(null, 0, "", new ValueList<Weapon>([new Weapon("laser", true, true, true)]), ValueList<Countermeasure>.Empty);
        model.Apply(new TelemetryStreamEvent(TelemetryStreamEventKind.Frame,
            new TelemetryFrame(DateTimeOffset.UnixEpoch, null, weapons, ValueList<GameEventType>.Empty, null, null, null)));
        for (var i = 0; i < TelemetryModel.MaxLogEntries + 10; i++)
            model.LogAction("v_lights_on", null, new ActionResult(ActionStatus.Ok, ""));
        model.Apply(new TelemetryStreamEvent(TelemetryStreamEventKind.Disconnected, Reason: "Unavailable"));

        var snapshot = model.GetSnapshot();
        Assert.Contains(snapshot.Fields, f => f.Path == "Weapons.Weapons[0].Name" && f.Value == "laser");
        Assert.Contains(snapshot.Fields, f => f.Path == "Weapons.Countermeasures" && f.Value == "(empty)");
        Assert.Equal(TelemetryModel.MaxLogEntries, snapshot.Log.Count);
        Assert.Equal("Disconnected: Unavailable", snapshot.Log[0].Text);
        Assert.Equal("v_lights_on → Ok", snapshot.Log[1].Text);
        Assert.StartsWith("Disconnected", snapshot.Status);
    }

    [Fact]
    public void SetupStatusTextDescribesConfigurationAndChanges()
    {
        var channel = new StarCitizen.LocalTelemetry.Setup.GameChannel("PTU", @"X:\missing\PTU", "5.0.0-ptu.1");
        var status = new StarCitizen.LocalTelemetry.Setup.TelemetrySetupStatus(channel, true, null);

        var text = SetupStatusText.Describe(status, TelemetrySchema.CheckGameVersion(channel));
        Assert.Contains("Channel: PTU (5.0.0-ptu.1)", text);
        Assert.Contains("user.cfg: not set", text);
        Assert.Contains("StarCitizen.exe not found", text);
        Assert.EndsWith("Telemetry is not configured.", text);
        Assert.Equal("Configured telemetry: unknown (not set in user.cfg)", SetupStatusText.DescribeState(status));
        Assert.Equal("Configured telemetry: enabled", SetupStatusText.DescribeState(status with { UserCfgEnabled = true }));
        Assert.Equal("Configured telemetry: disabled", SetupStatusText.DescribeState(status with { UserCfgEnabled = false }));
        Assert.Equal("Configured telemetry: incomplete (environment variable not enabled)",
            SetupStatusText.DescribeState(status with { UserCfgEnabled = true, EnvironmentVariableEnabled = false }));

        Assert.Equal("Enable: nothing to change.", SetupStatusText.Describe("Enable", new(false, false, null)));
        Assert.Equal(@"Enable: changed environment variable and user.cfg. Backup: X:\user.cfg.bak. Restart the RSI Launcher and the game.",
            SetupStatusText.Describe("Enable", new(true, true, @"X:\user.cfg.bak")));
    }

    [Fact]
    public void DisconnectIsLoggedOnceUntilConnectionIsRestored()
    {
        var model = new TelemetryModel();
        var disconnected = new TelemetryStreamEvent(TelemetryStreamEventKind.Disconnected, Reason: "Unavailable: Error connecting to subchannel.");
        model.Apply(disconnected);
        model.Apply(new TelemetryStreamEvent(TelemetryStreamEventKind.Connecting));
        model.Apply(new TelemetryStreamEvent(TelemetryStreamEventKind.Disconnected, Reason: "Another error"));
        var entry = Assert.Single(model.GetSnapshot().Log);
        Assert.Equal("Disconnected: Unavailable: Error connecting to subchannel.", entry.Text);
        Assert.Contains("Another error", model.GetSnapshot().Status);
        Assert.Contains("retrying", model.GetSnapshot().Status);

        model.Apply(new TelemetryStreamEvent(TelemetryStreamEventKind.Connected));
        model.Apply(disconnected);
        Assert.Equal(3, model.GetSnapshot().Log.Count);
        Assert.Equal(entry.Text, model.GetSnapshot().Log[0].Text);
    }

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
