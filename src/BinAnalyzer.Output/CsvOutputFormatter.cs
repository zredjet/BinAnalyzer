using System.Text;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Interfaces;

namespace BinAnalyzer.Output;

public sealed class CsvOutputFormatter : IOutputFormatter
{
    private readonly bool _useTsv;
    private readonly char _separator;

    public CsvOutputFormatter(bool useTsv = false)
    {
        _useTsv = useTsv;
        _separator = useTsv ? '\t' : ',';
    }

    public string Format(DecodedStruct root)
    {
        var sb = new StringBuilder();

        // ヘッダ行
        sb.Append("path").Append(_separator)
          .Append("type").Append(_separator)
          .Append("offset").Append(_separator)
          .Append("size").Append(_separator)
          .Append("value").Append(_separator)
          .Append("validation");
        sb.AppendLine();

        CollectLeafRows(sb, root, "");
        return sb.ToString();
    }

    private void CollectLeafRows(StringBuilder sb, DecodedNode node, string parentPath)
    {
        if (node.IsPadding)
            return;

        var path = parentPath.Length == 0 ? node.Name : $"{parentPath}.{node.Name}";

        switch (node)
        {
            case DecodedStruct structNode:
                foreach (var child in structNode.Children)
                    CollectLeafRows(sb, child, path);
                break;

            case DecodedArray arrayNode:
                for (var i = 0; i < arrayNode.Elements.Count; i++)
                {
                    var elementPath = $"{path}.{i}";
                    var element = arrayNode.Elements[i];
                    if (element is DecodedStruct elementStruct)
                    {
                        foreach (var child in elementStruct.Children)
                            CollectLeafRows(sb, child, elementPath);
                    }
                    else
                    {
                        // Leaf elements in array: write with indexed path
                        WriteRow(sb, elementPath, element);
                    }
                }
                break;

            case DecodedCompressed compressedNode:
                if (compressedNode.DecodedContent is not null)
                {
                    foreach (var child in compressedNode.DecodedContent.Children)
                        CollectLeafRows(sb, child, path);
                }
                else
                {
                    WriteRow(sb, path, compressedNode);
                }
                break;

            default:
                WriteRow(sb, path, node);
                break;
        }
    }

    private void WriteRow(StringBuilder sb, string path, DecodedNode node)
    {
        var (type, value) = GetTypeAndValue(node);

        var validationStr = node.Validation is { } v
            ? (v.Passed ? "✓" : "✗")
            : "";

        sb.Append(Escape(path)).Append(_separator)
          .Append(Escape(type)).Append(_separator)
          .Append(node.Offset).Append(_separator)
          .Append(node.Size).Append(_separator)
          .Append(Escape(value)).Append(_separator)
          .Append(Escape(validationStr));
        sb.AppendLine();
    }

    private static (string type, string value) GetTypeAndValue(DecodedNode node) => node switch
    {
        DecodedInteger intNode => ("integer", intNode.Value.ToString()),
        DecodedString strNode => ("string", strNode.Value),
        DecodedFloat floatNode => (floatNode.IsSinglePrecision ? "float32" : "float64", floatNode.Value.ToString("G")),
        DecodedBytes bytesNode => ("bytes", FormatBytes(bytesNode.RawBytes)),
        DecodedBitfield bitfieldNode => ("bitfield", $"0x{bitfieldNode.RawValue:X}"),
        DecodedFlags flagsNode => ("flags", $"0x{flagsNode.RawValue:X}"),
        DecodedVirtual virtualNode => ("virtual", virtualNode.Value.ToString() ?? ""),
        DecodedCompressed compressedNode => ("compressed", $"{compressedNode.Algorithm} ({compressedNode.CompressedSize} -> {compressedNode.DecompressedSize} bytes)"),
        DecodedError errorNode => ("error", errorNode.ErrorMessage),
        _ => ("unknown", ""),
    };

    private static string FormatBytes(ReadOnlyMemory<byte> bytes)
    {
        var span = bytes.Span;
        var sb = new StringBuilder(span.Length * 3);
        for (var i = 0; i < span.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(span[i].ToString("X2"));
        }
        return sb.ToString();
    }

    private string Escape(string value)
    {
        if (_useTsv)
        {
            // TSV: タブと改行をエスケープ
            return value.Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        // CSV: RFC 4180準拠
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
