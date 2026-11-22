using System.Text;
using StarCitizen.LocalTelemetry.Setup;

namespace StarCitizen.LocalTelemetry.Tests;

public sealed class SetupTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("sc-setup-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void ParsesLauncherLogNewestFirst()
    {
        string[] lines =
        [
            """{ "t":"1", "[main][info] ": "[PatcherPhase] Delta update completed (SC LIVE 4.3.1-live.1) in D:\\SC\\StarCitizen\\LIVE"  },""",
            """{ "t":"2", "[main][info] ": "[PatcherPhase] Starting delta update (SC PTU 5.0.0-ptu.13248297) in C:\\Games\\StarCitizen (statistics enabled: false)"  },""",
            """{ "t":"3", "[main][info] ": "[Launcher::launch] Launching Star Citizen PTU from (C:\\Games\\StarCitizen\\PTU)"  },"""
        ];
        var candidates = new List<string>();
        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        StarCitizenLocator.ParseLogLines(lines, candidates, versions);

        Assert.Equal([@"C:\Games\StarCitizen\PTU", @"D:\SC\StarCitizen\LIVE"], candidates);
        Assert.Equal("5.0.0-ptu.13248297", versions["PTU"]);
        Assert.Equal("4.3.1-live.1", versions["LIVE"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("PTU")]
    [InlineData(@"PTU\Bin64")]
    [InlineData(@"PTU\Bin64\StarCitizen.exe")]
    [InlineData("..")]
    public void FromPathResolvesTheInstallationFolder(string relative)
    {
        var sc = Path.Combine(_root, "StarCitizen");
        CreateChannel(sc, "PTU");
        CreateChannel(sc, "LIVE");
        Directory.CreateDirectory(Path.Combine(sc, "Logs"));

        var installation = StarCitizenLocator.FromPath(Path.Combine(sc, relative));

        Assert.NotNull(installation);
        Assert.Equal(sc, installation.RootPath);
        Assert.Equal(["LIVE", "PTU"], installation.Channels.Select(c => c.Name));
    }

    [Fact]
    public async Task FindsInstallationFromLogAndAddsVersions()
    {
        var sc = Path.Combine(_root, "Games", "StarCitizen");
        CreateChannel(sc, "PTU");
        var logs = Directory.CreateDirectory(Path.Combine(_root, "logs")).FullName;
        var escaped = sc.Replace(@"\", @"\\");
        await File.WriteAllTextAsync(Path.Combine(logs, "log.log"), $$"""{ "t":"1", "[main][info] ": "Delta update completed (SC PTU 5.0.0-ptu.1) in {{escaped}}\\PTU"  },""", TestContext.Current.CancellationToken);

        var installation = await StarCitizenLocator.FindAsync(logs, [], CancellationToken.None);

        Assert.NotNull(installation);
        Assert.Equal("5.0.0-ptu.1", Assert.Single(installation.Channels).Version);
    }

    [Fact]
    public void FromPathReturnsNullWithoutChannels()
    {
        Assert.Null(StarCitizenLocator.FromPath(_root));
        Assert.Null(StarCitizenLocator.FromPath(Path.Combine(_root, "missing")));
    }

    [Fact]
    public void EnablingUpdatesExistingLineAndKeepsTheRest()
    {
        var cfg = Path.Combine(_root, "user.cfg");
        File.WriteAllText(cfg, "#server.game.telemetry.enabled=1\r\nr_VSync = 0\r\nserver.game.telemetry.enabled = 0 ; note\r\n");

        var change = TelemetrySetup.WriteUserCfg(cfg, true);

        Assert.True(change.Changed);
        Assert.Equal(cfg + ".bak", change.BackupPath);
        Assert.Equal("#server.game.telemetry.enabled=1\r\nr_VSync = 0\r\nserver.game.telemetry.enabled = 1 ; note\r\n", File.ReadAllText(cfg));
        Assert.True(TelemetrySetup.ReadUserCfg(cfg));
        Assert.False(TelemetrySetup.WriteUserCfg(cfg, true).Changed);
    }

    [Fact]
    public void EnablingAppendsAndKeepsBomAndLineEndings()
    {
        var cfg = Path.Combine(_root, "user.cfg");
        File.WriteAllBytes(cfg, [.. Encoding.UTF8.Preamble, .. "r_VSync=0\n#comment"u8]);

        Assert.Null(TelemetrySetup.ReadUserCfg(cfg));
        Assert.True(TelemetrySetup.WriteUserCfg(cfg, true).Changed);

        Assert.Equal([.. Encoding.UTF8.Preamble, .. "r_VSync=0\n#comment\nserver.game.telemetry.enabled=1\n"u8], File.ReadAllBytes(cfg));
    }

    [Fact]
    public void DisablingWithoutFileOrVariableChangesNothing()
    {
        var cfg = Path.Combine(_root, "user.cfg");
        Assert.False(TelemetrySetup.WriteUserCfg(cfg, false).Changed);
        Assert.False(File.Exists(cfg));

        File.WriteAllText(cfg, "r_VSync=0\r\n");
        Assert.False(TelemetrySetup.WriteUserCfg(cfg, false).Changed);
        Assert.False(File.Exists(cfg + ".bak"));
    }

    [Fact]
    public void EnablingCreatesMissingFileAndDisablingSetsZero()
    {
        var cfg = Path.Combine(_root, "user.cfg");
        Assert.True(TelemetrySetup.WriteUserCfg(cfg, true).Changed);
        Assert.True(TelemetrySetup.ReadUserCfg(cfg));

        Assert.True(TelemetrySetup.WriteUserCfg(cfg, false).Changed);
        Assert.Equal("server.game.telemetry.enabled=0\r\n", File.ReadAllText(cfg));
    }

    [Fact]
    public async Task ResolveChannelFindsChannelOrExplainsWhatIsInstalled()
    {
        var sc = Path.Combine(_root, "StarCitizen");
        CreateChannel(sc, "LIVE");

        var live = await TelemetrySetup.ResolveChannelAsync("live", sc, CancellationToken.None);
        Assert.Equal(Path.Combine(sc, "LIVE", "user.cfg"), live.UserCfgPath);

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            TelemetrySetup.ResolveChannelAsync("PTU", sc, CancellationToken.None));
        Assert.Contains("Installed: LIVE.", error.Message);
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            TelemetrySetup.ResolveChannelAsync("PTU", Path.Combine(_root, "missing"), CancellationToken.None));
    }

    private static void CreateChannel(string root, string name)
    {
        var bin = Directory.CreateDirectory(Path.Combine(root, name, "Bin64")).FullName;
        File.WriteAllText(Path.Combine(bin, "StarCitizen.exe"), "");
    }
}
