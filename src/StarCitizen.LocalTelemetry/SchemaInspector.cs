using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace StarCitizen.LocalTelemetry;

/// <summary>
/// Finds fields and enum values in a parsed message that the bundled schema doesn't define. Generated messages keep
/// unknown fields and enum values and write them back, so the re-serialized bytes are walked against the descriptor.
/// </summary>
internal static class SchemaInspector
{
    public static IReadOnlyList<SchemaMismatch> Inspect(IMessage message)
    {
        var found = new List<SchemaMismatch>();
        Walk(new CodedInputStream(message.ToByteArray()), message.Descriptor, message.Descriptor.Name, found);
        return found;
    }

    private static void Walk(CodedInputStream input, MessageDescriptor descriptor, string path, List<SchemaMismatch> found)
    {
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            var number = WireFormat.GetTagFieldNumber(tag);
            var wireType = WireFormat.GetTagWireType(tag);
            var field = descriptor.FindFieldByNumber(number);
            if (field is null)
            {
                Add(found, new(SchemaMismatchKind.UnknownField, path, number));
                input.SkipLastField();
                continue;
            }

            var fieldPath = $"{path}.{field.Name}";
            var expected = ExpectedWireType(field.FieldType);
            var packed = field.IsRepeated && wireType == WireFormat.WireType.LengthDelimited &&
                expected != WireFormat.WireType.LengthDelimited;
            if (wireType != expected && !packed)
            {
                Add(found, new(SchemaMismatchKind.ChangedFieldType, fieldPath, number));
                input.SkipLastField();
                continue;
            }

            switch (field.FieldType)
            {
                case FieldType.Message:
                    Walk(new CodedInputStream(input.ReadBytes().ToByteArray()), field.MessageType, fieldPath, found);
                    break;
                case FieldType.Enum when packed:
                    var values = new CodedInputStream(input.ReadBytes().ToByteArray());
                    while (!values.IsAtEnd) CheckEnum(field.EnumType, values.ReadEnum(), fieldPath, found);
                    break;
                case FieldType.Enum:
                    CheckEnum(field.EnumType, input.ReadEnum(), fieldPath, found);
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }
    }

    private static void CheckEnum(EnumDescriptor descriptor, int value, string path, List<SchemaMismatch> found)
    {
        if (descriptor.FindValueByNumber(value) is null)
        {
            Add(found, new(SchemaMismatchKind.UnknownEnumValue, path, value));
        }
    }

    private static void Add(List<SchemaMismatch> found, SchemaMismatch mismatch)
    {
        if (!found.Contains(mismatch)) found.Add(mismatch);
    }

    private static WireFormat.WireType ExpectedWireType(FieldType type) => type switch
    {
        FieldType.Double or FieldType.Fixed64 or FieldType.SFixed64 => WireFormat.WireType.Fixed64,
        FieldType.Float or FieldType.Fixed32 or FieldType.SFixed32 => WireFormat.WireType.Fixed32,
        FieldType.String or FieldType.Bytes or FieldType.Message => WireFormat.WireType.LengthDelimited,
        FieldType.Group => WireFormat.WireType.StartGroup,
        _ => WireFormat.WireType.Varint
    };
}
