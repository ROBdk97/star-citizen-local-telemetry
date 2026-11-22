using GameActionsGenerator;

namespace StarCitizen.LocalTelemetry.Tests;

public class GameActionsGeneratorTests
{
    [Fact]
    public void RenderingTheKnownCatalogReproducesGameActionsFile()
    {
        var path = Path.Combine(FindRepoRoot(), "src", "StarCitizen.LocalTelemetry", "GameActions.cs");

        var rendered = GameActionsWriter.Render(GameActions.Known, "5.0.202.10025");

        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), rendered);
    }

    [Fact]
    public void RenderHandlesNewCategoriesAndNameCollisions()
    {
        var text = GameActionsWriter.Render(
        [
            new GameAction("v_new-thing", "new.category", true),
            new GameAction("new_thing", "new.category", false),
            new GameAction("v_2d_map", "new.category", false),
            new GameAction("v_flightready", "spaceship_general", false)
        ], "1.0");

        Assert.Contains("public static class NewCategory", text);
        Assert.Contains("public const string NewThing = \"new_thing\";", text);
        Assert.Contains("public const string NewThing2 = \"v_new-thing\";", text);
        Assert.Contains("public const string _2dMap = \"v_2d_map\";", text);
        Assert.Contains("public const string FlightReady = \"v_flightready\";", text);
        Assert.Contains("new(NewCategory.NewThing2, NewCategory.Category, true),", text);
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "StarCitizen.LocalTelemetry.slnx"))) return dir.FullName;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
