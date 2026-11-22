using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using StarCitizen.LocalTelemetry;

namespace TelemetryExplorer;

public sealed record FieldRow(string Section, string Path, string Value, string Type, DateTimeOffset LastChanged);

public sealed record LogEntry(DateTimeOffset Time, string Kind, string Text);

public sealed record ModelSnapshot(
    long Version,
    string Status,
    long FrameCount,
    DateTimeOffset? LastFrameAt,
    IReadOnlyList<string> Sections,
    IReadOnlyList<FieldRow> Fields,
    IReadOnlyList<LogEntry> Log,
    string Json);

/// <summary>Thread-safe, UI-free state for the explorer. The stream writes it; the UI polls snapshots.</summary>
public sealed class TelemetryModel(TimeProvider? timeProvider = null)
{
    public const int MaxLogEntries = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly Lock _gate = new();
    private readonly SortedDictionary<string, object> _sections = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, FieldRow> _fields = new(StringComparer.Ordinal);
    private readonly LinkedList<LogEntry> _log = new();
    private long _version;
    private string _status = "Not connected";
    private long _frameCount;
    private DateTimeOffset? _lastFrameAt;
    private ModelSnapshot? _cached;

    public long Version
    {
        get { lock (_gate) return _version; }
    }

    public void Apply(TelemetryStreamEvent streamEvent)
    {
        ArgumentNullException.ThrowIfNull(streamEvent);
        lock (_gate)
        {
            switch (streamEvent.Kind)
            {
                case TelemetryStreamEventKind.Connecting:
                    _status = "Connecting…";
                    break;
                case TelemetryStreamEventKind.Connected:
                    _status = "Connected";
                    AddLog("Connection", "Connected");
                    break;
                case TelemetryStreamEventKind.Disconnected:
                    _status = $"Disconnected ({streamEvent.Reason}), retrying…";
                    AddLog("Connection", $"Disconnected: {streamEvent.Reason}");
                    break;
                case TelemetryStreamEventKind.SchemaMismatch:
                    AddLog("Schema", $"{streamEvent.Mismatch} (schema is for StarCitizen.exe {TelemetrySchema.BundledGameVersion})");
                    break;
                case TelemetryStreamEventKind.Frame when streamEvent.Frame is { } frame:
                    ApplyFrame(frame);
                    break;
            }

            Changed();
        }
    }

    public void LogAction(string actionName, TimeSpan? hold, ActionResult? result, string? error = null)
    {
        var holdText = hold is { } h ? $" (hold {h.TotalMilliseconds:0} ms)" : "";
        var outcome = error ?? (result!.Message is { Length: > 0 } m ? $"{result.Status}: {m}" : result.Status.ToString());
        lock (_gate)
        {
            AddLog("Action", $"{actionName}{holdText} → {outcome}");
            Changed();
        }
    }

    public void SetStatus(string status)
    {
        lock (_gate)
        {
            _status = status;
            Changed();
        }
    }

    public ModelSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return _cached ??= new ModelSnapshot(
                _version,
                _status,
                _frameCount,
                _lastFrameAt,
                [.. _sections.Keys],
                [.. _fields.Values],
                [.. _log],
                JsonSerializer.Serialize(_sections, JsonOptions));
        }
    }

    private void ApplyFrame(TelemetryFrame frame)
    {
        _frameCount++;
        _lastFrameAt = frame.ReceivedAt;
        Section(nameof(frame.Vehicle), frame.Vehicle);
        Section(nameof(frame.Weapons), frame.Weapons);
        Section(nameof(frame.Actor), frame.Actor);
        Section(nameof(frame.ForceFeedback), frame.ForceFeedback);
        Section(nameof(frame.Turret), frame.Turret);
        foreach (var gameEvent in frame.Events) AddLog("Game event", gameEvent.ToString());
    }

    // Sections absent from a frame keep their last known values, because the game sends partial frames.
    private void Section(string name, object? value)
    {
        if (value is null) return;
        _sections[name] = value;
        var now = _time.GetUtcNow();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (path, text, type) in Flatten(name, value, value.GetType(), 0))
        {
            seen.Add(path);
            if (!_fields.TryGetValue(path, out var old) || old.Value != text)
                _fields[path] = new FieldRow(name, path, text, type, now);
        }

        foreach (var stale in _fields.Keys.Where(k => k.StartsWith(name + ".", StringComparison.Ordinal) && !seen.Contains(k)).ToList())
            _fields.Remove(stale);
    }

    private void AddLog(string kind, string text)
    {
        _log.AddFirst(new LogEntry(_time.GetUtcNow(), kind, text));
        while (_log.Count > MaxLogEntries) _log.RemoveLast();
    }

    private void Changed()
    {
        _version++;
        _cached = null;
    }

    internal static IEnumerable<(string Path, string Value, string Type)> Flatten(string path, object? value, Type declared, int depth)
    {
        var type = Nullable.GetUnderlyingType(declared) ?? declared;
        if (value is null)
        {
            yield return (path, "—", TypeName(type));
            yield break;
        }

        if (IsLeaf(value.GetType()))
        {
            yield return (path, FormatLeaf(value), TypeName(value.GetType()));
            yield break;
        }

        if (depth > 8) yield break;
        if (value is IEnumerable items)
        {
            var index = 0;
            foreach (var item in items)
            {
                foreach (var row in Flatten($"{path}[{index}]", item, item?.GetType() ?? typeof(object), depth + 1))
                    yield return row;
                index++;
            }

            if (index == 0) yield return (path, "(empty)", "list");
            yield break;
        }

        foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0) continue;
            foreach (var row in Flatten($"{path}.{property.Name}", property.GetValue(value), property.PropertyType, depth + 1))
                yield return row;
        }
    }

    private static bool IsLeaf(Type type) =>
        type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) ||
        type == typeof(DateTimeOffset) || type == typeof(DateTime) || type == typeof(TimeSpan);

    private static string FormatLeaf(object value) => value switch
    {
        float f => f.ToString("0.###", CultureInfo.InvariantCulture),
        double d => d.ToString("0.###", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };

    private static string TypeName(Type type) => type switch
    {
        _ when type == typeof(float) => "float",
        _ when type == typeof(double) => "double",
        _ when type == typeof(bool) => "bool",
        _ when type == typeof(int) => "int",
        _ when type == typeof(uint) => "uint",
        _ when type == typeof(string) => "string",
        _ => type.Name
    };
}
