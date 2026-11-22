using System.Runtime.Versioning;
using Aprillz.MewUI;
using StarCitizen.LocalTelemetry;
using TelemetryExplorer;

var address = StarCitizenTelemetryClient.DefaultAddress;
var index = Array.IndexOf(args, "--address");
if (index >= 0 && (index + 1 >= args.Length || !Uri.TryCreate(args[index + 1], UriKind.Absolute, out address)))
{
    Console.Error.WriteLine("Usage: TelemetryExplorer [--address http://127.0.0.1:2942]");
    return 2;
}

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("The Telemetry Explorer runs only on Windows.");
    return 1;
}

using var client = new StarCitizenTelemetryClient(address);
Run(client, address!);
return 0;

[SupportedOSPlatform("windows")]
static void Run(StarCitizenTelemetryClient client, Uri address)
{
    var mainWindow = new MainWindow(client, address);
    Application.Create()
        .UseWin32()
        .UseDirect2D()
        .BuildMainWindow(mainWindow.Build)
        .Run();
}
