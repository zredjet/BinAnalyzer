using System.Text;
using BinAnalyzer.Core.Interfaces;
using BinAnalyzer.Core.Models;

namespace BinAnalyzer.Output;

public class MermaidSchemaFormatter : ISchemaFormatter
{
    public string Format(FormatDefinition format)
    {
        var sb = new StringBuilder();
        sb.AppendLine("classDiagram");

        // クラス定義
        foreach (var (name, structDef) in format.Structs)
        {
            var safeName = Sanitize(name);
            sb.AppendLine($"    class {safeName} {{");

            if (name == format.RootStruct)
                sb.AppendLine("        <<root>>");

            foreach (var field in structDef.Fields)
            {
                var typeStr = GetFieldTypeDisplay(field);
                sb.AppendLine($"        {typeStr} {field.Name}");
            }

            sb.AppendLine("    }");
        }

        // エッジ
        foreach (var (name, structDef) in format.Structs)
        {
            var safeName = Sanitize(name);

            foreach (var field in structDef.Fields)
            {
                var edges = CollectEdges(safeName, field);
                foreach (var edge in edges)
                    sb.AppendLine(edge);
            }
        }

        return sb.ToString();
    }

    private static List<string> CollectEdges(string fromNode, FieldDefinition field)
    {
        var edges = new List<string>();
        var multiplicity = IsRepeat(field) ? " *" : "";

        if (field.Type == FieldType.Switch)
        {
            // switch cases
            if (field.SwitchCases is { } cases)
            {
                foreach (var sc in cases)
                {
                    var targetNode = Sanitize(sc.StructRef);
                    var condText = sc.Condition.OriginalText;
                    var label = $"{field.Name} [{condText}]{multiplicity}";
                    edges.Add($"    {fromNode} --> {targetNode} : \"{label}\"");
                }
            }

            // switch default
            if (field.SwitchDefault is { } defaultRef)
            {
                var targetNode = Sanitize(defaultRef);
                var label = $"{field.Name} [default]{multiplicity}";
                edges.Add($"    {fromNode} --> {targetNode} : \"{label}\"");
            }
        }
        else if (field.StructRef is { } structRef)
        {
            // struct 参照 (直接 or 圧縮)
            var targetNode = Sanitize(structRef);
            var compression = IsCompression(field.Type) ? " (圧縮)" : "";
            var label = $"{field.Name}{compression}{multiplicity}";
            edges.Add($"    {fromNode} --> {targetNode} : \"{label}\"");
        }

        return edges;
    }

    private static string GetFieldTypeDisplay(FieldDefinition field)
    {
        if (field.Type == FieldType.Struct)
        {
            var suffix = IsRepeat(field) ? "[]" : "";
            return $"struct{suffix}";
        }

        if (field.Type == FieldType.Switch)
            return "switch";

        var typeName = FieldTypeToString(field.Type);
        if (IsRepeat(field))
            typeName += "[]";
        return typeName;
    }

    private static string FieldTypeToString(FieldType type)
    {
        return type switch
        {
            FieldType.UInt8 => "uint8",
            FieldType.UInt16 => "uint16",
            FieldType.UInt32 => "uint32",
            FieldType.UInt64 => "uint64",
            FieldType.Int8 => "int8",
            FieldType.Int16 => "int16",
            FieldType.Int32 => "int32",
            FieldType.Int64 => "int64",
            FieldType.Bytes => "bytes",
            FieldType.Ascii => "ascii",
            FieldType.Utf8 => "utf8",
            FieldType.Utf16Le => "utf16le",
            FieldType.Utf16Be => "utf16be",
            FieldType.ShiftJis => "shiftjis",
            FieldType.Latin1 => "latin1",
            FieldType.AsciiZ => "asciiz",
            FieldType.Utf8Z => "utf8z",
            FieldType.Float32 => "float32",
            FieldType.Float64 => "float64",
            FieldType.Struct => "struct",
            FieldType.Switch => "switch",
            FieldType.Bitfield => "bitfield",
            FieldType.Zlib => "zlib",
            FieldType.Deflate => "deflate",
            FieldType.Gzip => "gzip",
            FieldType.Bzip2 => "bzip2",
            FieldType.Lzma => "lzma",
            FieldType.Zstd => "zstd",
            FieldType.Lz4 => "lz4",
            FieldType.Virtual => "virtual",
            FieldType.ULeb128 => "uleb128",
            FieldType.SLeb128 => "sleb128",
            FieldType.Vlq => "vlq",
            _ => type.ToString().ToLowerInvariant(),
        };
    }

    private static bool IsRepeat(FieldDefinition field) =>
        field.Repeat is not RepeatMode.None;

    private static bool IsCompression(FieldType type) =>
        type is FieldType.Zlib or FieldType.Deflate or FieldType.Gzip
            or FieldType.Bzip2 or FieldType.Lzma or FieldType.Zstd or FieldType.Lz4;

    private static string Sanitize(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");
}
