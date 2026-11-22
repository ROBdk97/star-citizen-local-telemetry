namespace StarCitizen.LocalTelemetry;

/// <summary>The kind of a <see cref="SchemaMismatch"/>.</summary>
public enum SchemaMismatchKind
{
    /// <summary>The game sent a field the bundled schema doesn't define; its value is not mapped.</summary>
    UnknownField,
    /// <summary>The game sent an enum value the bundled schema doesn't define; it is mapped as an undefined enum value.</summary>
    UnknownEnumValue,
    /// <summary>The game sent a known field with a different wire type; its value is not mapped.</summary>
    ChangedFieldType
}

/// <summary>
/// A difference between what the game sent and the schema bundled with this library
/// (see <see cref="TelemetrySchema.BundledGameVersion"/>). Usually means the game was patched; see
/// <c>tools/README.md</c> for updating the schema.
/// </summary>
/// <param name="Kind">What differs.</param>
/// <param name="Path">
/// The protobuf message path, for example <c>TelemetryStreamResponse.vehicle.state</c>; for
/// <see cref="SchemaMismatchKind.UnknownEnumValue"/> and <see cref="SchemaMismatchKind.ChangedFieldType"/>
/// it ends with the field name.
/// </param>
/// <param name="Number">The unknown or changed field number, or the unknown enum value.</param>
public sealed record SchemaMismatch(SchemaMismatchKind Kind, string Path, long Number)
{
    /// <summary>Returns a readable description, for example <c>TelemetryStreamResponse.vehicle has unknown field 42</c>.</summary>
    /// <returns>The description.</returns>
    public override string ToString() => Kind switch
    {
        SchemaMismatchKind.UnknownField => $"{Path} has unknown field {Number}",
        SchemaMismatchKind.UnknownEnumValue => $"{Path} has unknown enum value {Number}",
        _ => $"{Path} (field {Number}) has a different type"
    };
}
