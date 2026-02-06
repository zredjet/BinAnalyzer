using System.Text;
using System.Text.Json;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Interfaces;

namespace BinAnalyzer.Output;

public sealed class JsonOutputFormatter : IOutputFormatter
{
    public string Format(DecodedStruct root)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        WriteStructNode(writer, root);
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteNode(Utf8JsonWriter writer, DecodedNode node)
    {
        switch (node)
        {
            case DecodedStruct structNode:
                WriteStructNode(writer, structNode);
                break;
            case DecodedArray arrayNode:
                WriteArrayNode(writer, arrayNode);
                break;
            case DecodedInteger intNode:
                WriteIntegerNode(writer, intNode);
                break;
            case DecodedBytes bytesNode:
                WriteBytesNode(writer, bytesNode);
                break;
            case DecodedString stringNode:
                WriteStringNode(writer, stringNode);
                break;
            case DecodedFloat floatNode:
                WriteFloatNode(writer, floatNode);
                break;
            case DecodedFlags flagsNode:
                WriteFlagsNode(writer, flagsNode);
                break;
            case DecodedBitfield bitfieldNode:
                WriteBitfieldNode(writer, bitfieldNode);
                break;
            case DecodedCompressed compressedNode:
                WriteCompressedNode(writer, compressedNode);
                break;
            case DecodedVirtual virtualNode:
                WriteVirtualNode(writer, virtualNode);
                break;
        }
    }

    private static void WriteCommonProperties(Utf8JsonWriter writer, DecodedNode node, string type)
    {
        writer.WriteString("_type", type);
        writer.WriteNumber("offset", node.Offset);
        writer.WriteNumber("size", node.Size);
        if (node.Description is not null)
            writer.WriteString("description", node.Description);
    }

    private static void WriteStructNode(Utf8JsonWriter writer, DecodedStruct node)
    {
        writer.WriteStartObject();
        WriteCommonProperties(writer, node, "struct");
        writer.WriteString("name", node.Name);
        writer.WriteString("struct_type", node.StructType);

        writer.WritePropertyName("children");
        writer.WriteStartObject();
        foreach (var child in node.Children)
        {
            writer.WritePropertyName(child.Name);
            WriteNode(writer, child);
        }
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private static void WriteArrayNode(Utf8JsonWriter writer, DecodedArray node)
    {
        writer.WriteStartObject();
        WriteCommonProperties(writer, node, "array");
        writer.WriteString("name", node.Name);
        writer.WriteNumber("count", node.Elements.Count);

        writer.WritePropertyName("elements");
        writer.WriteStartArray();
        foreach (var element in node.Elements)
        {
            WriteNode(writer, element);
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    private static void WriteIntegerNode(Utf8JsonWriter writer, DecodedInteger node)
    {
        writer.WriteStartObject();
        WriteCommonProperties(writer, node, "integer");
        writer.WriteString("name", node.Name);
        writer.WriteNumber("value", node.Value);
        writer.WriteString("hex", $"0x{node.Value:X}");

        if (node.EnumLabel is not null)
        {
            writer.WriteString("enum_label", node.EnumLabel);
            if (node.EnumDescription is not null)
                writer.WriteString("enum_description", node.EnumDescription);
        }

        if (node.ChecksumValid.HasValue)
        {
            writer.WriteBoolean("checksum_valid", node.ChecksumValid.Value);
            if (node.ChecksumExpected.HasValue)
                writer.WriteString("checksum_expected", $"0x{node.ChecksumExpected:X}");
        }

        writer.WriteEndObject();
    }

    private static void WriteBytesNode(Utf8JsonWriter writer, DecodedBytes node)
    {
        writer.WriteStartObject();
        WriteCommonProperties(writer, node, "bytes");
        writer.WriteString("name", node.Name);

        var span = node.RawBytes.Span;
        var sb = new StringBuilder(span.Length * 3);
        for (var i = 0; i < span.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(span[i].ToString("X2"));
        }
        writer.WriteString("hex", sb.ToString());

        if (node.ValidationPassed.HasValue)
            writer.WriteBoolean("valid", node.ValidationPassed.Value);

        writer.WriteEndObject();
    }

    private static void WriteStringNode(Utf8JsonWriter writer, DecodedString node)
    {
        writer.WriteStartObject();
        WriteCommonProperties(writer, node, "string");
        writer.WriteString("name", node.Name);
        writer.WriteString("value", node.Value);
        writer.WriteString("encoding", node.Encoding);

        if (node.Flags is { Count: > 0 })
        {
            writer.WritePropertyName("flags");
            writer.WriteStartObject();
            foreach (var flag in node.Flags)
            {
                writer.WritePropertyName(flag.Name);
                writer.WriteStartObject();
                writer.WriteBoolean("set", flag.IsSet);
                if (flag.Meaning is not null)
                    writer.WriteString("meaning", flag.Meaning);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteFloatNode(Utf8JsonWriter writer, DecodedFloat node)
    {
        writer.WriteStartObject();
        WriteCommonProperties(writer, node, node.IsSinglePrecision ? "float32" : "float64");
        writer.WriteString("name", node.Name);
        if (double.IsNaN(node.Value) || double.IsInfinity(node.Value))
            writer.WriteString("value", node.Value.ToString());
        else
            writer.WriteNumber("value", node.Value);
        writer.WriteEndObject();
    }

    private static void WriteFlagsNode(Utf8JsonWriter writer, DecodedFlags node)
    {
        writer.WriteStartObject();
        WriteCommonProperties(writer, node, "flags");
        writer.WriteString("name", node.Name);
        writer.WriteString("raw_value", $"0x{node.RawValue:X}");

        writer.WritePropertyName("flags");
        writer.WriteStartObject();
        foreach (var flag in node.FlagStates)
        {
            writer.WritePropertyName(flag.Name);
            writer.WriteStartObject();
            writer.WriteBoolean("set", flag.IsSet);
            if (flag.Meaning is not null)
                writer.WriteString("meaning", flag.Meaning);
            writer.WriteEndObject();
        }
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private static void WriteBitfieldNode(Utf8JsonWriter writer, DecodedBitfield node)
    {
        writer.WriteStartObject();
        WriteCommonProperties(writer, node, "bitfield");
        writer.WriteString("name", node.Name);
        writer.WriteString("raw_value", $"0x{node.RawValue:X}");

        writer.WritePropertyName("fields");
        writer.WriteStartArray();
        foreach (var field in node.Fields)
        {
            writer.WriteStartObject();
            writer.WriteString("name", field.Name);
            writer.WriteNumber("value", field.Value);
            writer.WriteNumber("bit_high", field.BitHigh);
            writer.WriteNumber("bit_low", field.BitLow);

            if (field.EnumLabel is not null)
            {
                writer.WriteString("enum_label", field.EnumLabel);
                if (field.EnumDescription is not null)
                    writer.WriteString("enum_description", field.EnumDescription);
            }

            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    private static void WriteVirtualNode(Utf8JsonWriter writer, DecodedVirtual node)
    {
        writer.WriteStartObject();
        WriteCommonProperties(writer, node, "virtual");
        writer.WriteString("name", node.Name);
        switch (node.Value)
        {
            case long l:
                writer.WriteNumber("value", l);
                break;
            case int i:
                writer.WriteNumber("value", i);
                break;
            case double d:
                if (double.IsNaN(d) || double.IsInfinity(d))
                    writer.WriteString("value", d.ToString());
                else
                    writer.WriteNumber("value", d);
                break;
            case bool b:
                writer.WriteBoolean("value", b);
                break;
            case string s:
                writer.WriteString("value", s);
                break;
            default:
                writer.WriteString("value", node.Value.ToString());
                break;
        }
        writer.WriteEndObject();
    }

    private static void WriteCompressedNode(Utf8JsonWriter writer, DecodedCompressed node)
    {
        writer.WriteStartObject();
        WriteCommonProperties(writer, node, "compressed");
        writer.WriteString("name", node.Name);
        writer.WriteString("algorithm", node.Algorithm);
        writer.WriteNumber("compressed_size", node.CompressedSize);
        writer.WriteNumber("decompressed_size", node.DecompressedSize);

        if (node.DecodedContent is not null)
        {
            writer.WritePropertyName("content");
            WriteStructNode(writer, node.DecodedContent);
        }
        else if (node.RawDecompressed is { } raw)
        {
            var span = raw.Span;
            var sb = new StringBuilder(span.Length * 3);
            for (var i = 0; i < span.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(span[i].ToString("X2"));
            }
            writer.WriteString("decompressed_hex", sb.ToString());
        }

        writer.WriteEndObject();
    }
}
