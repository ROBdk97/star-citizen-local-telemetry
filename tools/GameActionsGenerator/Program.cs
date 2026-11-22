using System.Text.RegularExpressions;
using GameActionsGenerator;
using StarCitizen.LocalTelemetry;

// Regenerates GameActions.cs from the game's ListTriggerableActions response. Never triggers actions.
string? output = null, version = null, proto = null;
var address = StarCitizenTelemetryClient.DefaultAddress;
var check = false;
for (var i = 0; i < args.Length; i++)
{
    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");
    switch (args[i])
    {
        case "--output": output = Next(); break;
        case "--version": version = Next(); break;
        case "--proto": proto = Next(); break;
        case "--address": address = new Uri(Next()); break;
        case "--check": check = true; break;
        default:
            Console.Error.WriteLine(
                "Usage: GameActionsGenerator --output <GameActions.cs> [--proto <schema.proto> | --version <x.y.z>] [--address <uri>] [--check]");
            return 2;
    }
}

if (output is null)
{
    Console.Error.WriteLine("--output is required.");
    return 2;
}

if (version is null && proto is not null &&
    Regex.Match(File.ReadAllText(proto), @"file version\s+([\d.]+\d)") is { Success: true } match)
    version = match.Groups[1].Value;
if (version is null)
{
    Console.Error.WriteLine("Pass --version, or --proto with a schema that states its file version.");
    return 2;
}

IReadOnlyList<GameAction> actions;
using (var client = new StarCitizenTelemetryClient(address))
{
    try
    {
        actions = await client.GetActionsAsync();
    }
    catch (Grpc.Core.RpcException ex)
    {
        Console.Error.WriteLine($"Could not list actions from {address} ({ex.StatusCode}). Is the game running with telemetry enabled?");
        return 1;
    }
}

if (actions.Count == 0)
{
    Console.Error.WriteLine("The game returned no actions; keeping the existing catalog.");
    return 1;
}

var oldNames = GameActions.Known.Select(a => a.Name).ToHashSet(StringComparer.Ordinal);
var newNames = actions.Select(a => a.Name).ToHashSet(StringComparer.Ordinal);
foreach (var added in newNames.Except(oldNames).Order(StringComparer.Ordinal)) Console.WriteLine($"+ {added}");
foreach (var removed in oldNames.Except(newNames).Order(StringComparer.Ordinal)) Console.WriteLine($"- {removed}");
var changedHold = actions.Where(a => GameActions.Known.Any(k => k.Name == a.Name && (k.Holdable != a.Holdable || k.Category != a.Category)));
foreach (var changed in changedHold) Console.WriteLine($"~ {changed.Name} (category={changed.Category}, holdable={changed.Holdable})");

var text = GameActionsWriter.Render(actions, version);
var current = File.Exists(output) ? File.ReadAllText(output).ReplaceLineEndings("\n") : null;
if (current == text)
{
    Console.WriteLine($"{actions.Count} actions; {output} is up to date.");
    return 0;
}

if (check)
{
    Console.WriteLine($"{actions.Count} actions; {output} is out of date.");
    return 1;
}

File.WriteAllText(output, text);
Console.WriteLine($"{actions.Count} actions written to {output}.");
return 0;
