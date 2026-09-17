using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Presentation;

/// <summary>詳細行の種別。表示側でスタイルを変えるためのヒント（TUI は無視してよい）。</summary>
public enum DetailRowKind
{
    /// <summary>通常テキスト</summary>
    Text,
    /// <summary>等幅で表示すべき値（オフセット、16進、生バイト）</summary>
    Mono,
    /// <summary>検証結果（PASS/FAIL）</summary>
    Validation,
    /// <summary>子要素（フラグ・ビットフィールドの個々の項目）</summary>
    Child,
}

public sealed record DetailRow(string Key, string Value, DetailRowKind Kind = DetailRowKind.Text);

/// <summary>選択ノードの詳細（Key/Value 行）。TUI 詳細ペインと GUI インスペクターで共通。</summary>
public static class NodeDetailFormatter
{
    public static IReadOnlyList<DetailRow> Format(DecodedNode node)
    {
        var details = new List<DetailRow>
        {
            new("Name", node.Name),
            new("Offset", $"0x{node.Offset:X8} ({node.Offset})", DetailRowKind.Mono),
            new("Size", $"{node.Size} bytes", DetailRowKind.Mono),
        };

        switch (node)
        {
            case DecodedInteger intNode:
                details.Add(new("Type", "integer"));
                details.Add(new("Value", intNode.Value.ToString(), DetailRowKind.Mono));
                if (intNode.Value is >= 16 or <= -16)
                    details.Add(new("Hex", $"0x{intNode.Value:X}", DetailRowKind.Mono));
                if (intNode.EnumLabel is not null)
                    details.Add(new("Enum", intNode.EnumLabel));
                if (intNode.EnumDescription is not null)
                    details.Add(new("Description", intNode.EnumDescription));
                if (intNode.ChecksumValid.HasValue)
                    details.Add(new("Checksum", intNode.ChecksumValid.Value ? "valid" : "invalid", DetailRowKind.Validation));
                if (intNode.StringTableValue is not null)
                    details.Add(new("String", intNode.StringTableValue));
                break;

            case DecodedFloat floatNode:
                details.Add(new("Type", floatNode.IsSinglePrecision ? "float32" : "float64"));
                details.Add(new("Value", floatNode.Value.ToString("G"), DetailRowKind.Mono));
                break;

            case DecodedString strNode:
                details.Add(new("Type", "string"));
                details.Add(new("Value", strNode.Value, DetailRowKind.Mono));
                details.Add(new("Encoding", strNode.Encoding));
                break;

            case DecodedBytes bytesNode:
                details.Add(new("Type", "bytes"));
                details.Add(new("Hex", FormatHexPreview(bytesNode.RawBytes.Span), DetailRowKind.Mono));
                if (bytesNode.ChecksumValid.HasValue)
                    details.Add(new("Checksum", bytesNode.ChecksumValid.Value ? "valid" : "invalid", DetailRowKind.Validation));
                break;

            case DecodedStruct structNode:
                details.Add(new("Type", "struct"));
                details.Add(new("StructType", structNode.StructType));
                details.Add(new("Children", $"{structNode.Children.Count} fields"));
                break;

            case DecodedArray arrayNode:
                details.Add(new("Type", "array"));
                details.Add(new("Elements", $"{arrayNode.Elements.Count} items"));
                break;

            case DecodedFlags flagsNode:
                details.Add(new("Type", "flags"));
                details.Add(new("RawValue", $"0x{flagsNode.RawValue:X}", DetailRowKind.Mono));
                foreach (var flag in flagsNode.FlagStates)
                    details.Add(new($"  {flag.Name}", flag.Meaning ?? (flag.IsSet ? "set" : "clear"), DetailRowKind.Child));
                break;

            case DecodedBitfield bitfieldNode:
                details.Add(new("Type", "bitfield"));
                details.Add(new("RawValue", $"0x{bitfieldNode.RawValue:X}", DetailRowKind.Mono));
                foreach (var field in bitfieldNode.Fields)
                {
                    var bits = field.BitHigh == field.BitLow
                        ? $"bit {field.BitLow}"
                        : $"bits {field.BitHigh}:{field.BitLow}";
                    details.Add(new($"  {field.Name}", $"{field.Value} ({bits})", DetailRowKind.Child));
                }
                break;

            case DecodedCompressed compNode:
                details.Add(new("Type", "compressed"));
                details.Add(new("Algorithm", compNode.Algorithm));
                details.Add(new("Compressed", $"{compNode.CompressedSize} bytes", DetailRowKind.Mono));
                details.Add(new("Decompressed", $"{compNode.DecompressedSize} bytes", DetailRowKind.Mono));
                break;

            case DecodedVirtual virtualNode:
                details.Add(new("Type", "virtual"));
                details.Add(new("Value", virtualNode.Value?.ToString() ?? "", DetailRowKind.Mono));
                break;

            case DecodedError errorNode:
                details.Add(new("Type", "error"));
                details.Add(new("Error", errorNode.ErrorMessage));
                break;
        }

        if (node.Validation is { } v)
            details.Add(new("Validation", v.Passed ? $"PASS ({v.Expression})" : $"FAIL ({v.Expression})", DetailRowKind.Validation));

        return details;
    }

    private static string FormatHexPreview(ReadOnlySpan<byte> span)
    {
        var count = Math.Min(span.Length, 16);
        var parts = new string[count];
        for (var i = 0; i < count; i++)
            parts[i] = span[i].ToString("X2");
        var result = string.Join(" ", parts);
        if (span.Length > 16)
            result += " ...";
        return result;
    }
}
