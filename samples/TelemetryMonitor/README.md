# Telemetry Monitor

A minimal, read-only console sample. It uses `ObserveTelemetryAsync` to report
connection changes, and prints the vehicle name and light state whenever either
changes.

```powershell
dotnet run --project .\samples\TelemetryMonitor\TelemetryMonitor.csproj
```

Press Ctrl+C to stop. It never triggers actions.
