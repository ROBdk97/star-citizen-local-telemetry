using StarCitizen.LocalTelemetry;

using var stop = new CancellationTokenSource();
using var client = new StarCitizenTelemetryClient();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stop.Cancel();
};

string? lastName = null;
bool? lastLights = null;
try
{
    await foreach (var update in client.ObserveTelemetryAsync(TimeSpan.FromSeconds(2), stop.Token))
    {
        switch (update.Kind)
        {
            case TelemetryStreamEventKind.Connecting:
                Console.WriteLine("Connecting...");
                break;
            case TelemetryStreamEventKind.Connected:
                Console.WriteLine("Receiving telemetry.");
                break;
            case TelemetryStreamEventKind.Disconnected:
                Console.WriteLine($"Disconnected ({update.Reason}); retrying.");
                lastName = null;
                lastLights = null;
                break;
            case TelemetryStreamEventKind.SchemaMismatch:
                Console.WriteLine($"Schema mismatch: {update.Mismatch} (schema is for StarCitizen.exe {TelemetrySchema.BundledGameVersion}).");
                break;
            case TelemetryStreamEventKind.Frame:
                var vehicle = update.Frame?.Vehicle;
                if (vehicle is not null && (vehicle.Name != lastName || vehicle.LightsOn != lastLights))
                {
                    Console.WriteLine($"Vehicle: {vehicle.Name}; lights: {vehicle.LightsOn}");
                    lastName = vehicle.Name;
                    lastLights = vehicle.LightsOn;
                }
                break;
        }
    }
}
catch (OperationCanceledException) when (stop.IsCancellationRequested)
{
    Console.WriteLine("Stopped.");
}
