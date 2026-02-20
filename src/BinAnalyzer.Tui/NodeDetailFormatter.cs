using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Tui;

internal static class NodeDetailFormatter
{
    internal static List<(string Key, string Value)> Format(DecodedNode node)
    {
        var details = new List<(string Key, string Value)>
        {
            ("Name", node.Name),
            ("Offset", $"0x{node.Offset:X8} ({node.Offset})"),
            ("Size", $"{node.Size} bytes"),
        };

        switch (node)
        {
            case DecodedInteger intNode:
                details.Add(("Type", "integer"));
                details.Add(("Value", intNode.Value.ToString()));
                if (intNode.Value is >= 16 or <= -16)
                    details.Add(("Hex", $"0x{intNode.Value:X}"));
                if (intNode.EnumLabel is not null)
                    details.Add(("Enum", intNode.EnumLabel));
                if (intNode.EnumDescription is not null)
                    details.Add(("Description", intNode.EnumDescription));
                if (intNode.ChecksumValid.HasValue)
                    details.Add(("Checksum", intNode.ChecksumValid.Value ? "valid" : "invalid"));
                if (intNode.StringTableValue is not null)
                    details.Add(("String", intNode.StringTableValue));
                break;

            case DecodedFloat floatNode:
                details.Add(("Type", floatNode.IsSinglePrecision ? "float32" : "float64"));
                details.Add(("Value", floatNode.Value.ToString("G")));
                break;

            case DecodedString strNode:
                details.Add(("Type", "string"));
                details.Add(("Value", strNode.Value));
                details.Add(("Encoding", strNode.Encoding));
                break;

            case DecodedBytes bytesNode:
                details.Add(("Type", "bytes"));
                details.Add(("Hex", FormatHexPreview(bytesNode.RawBytes.Span)));
                if (bytesNode.ChecksumValid.HasValue)
                    details.Add(("Checksum", bytesNode.ChecksumValid.Value ? "valid" : "invalid"));
                break;

            case DecodedStruct structNode:
                details.Add(("Type", "struct"));
                details.Add(("StructType", structNode.StructType));
                details.Add(("Children", $"{structNode.Children.Count} fields"));
                break;

            case DecodedArray arrayNode:
                details.Add(("Type", "array"));
                details.Add(("Elements", $"{arrayNode.Elements.Count} items"));
                break;

            case DecodedFlags flagsNode:
                details.Add(("Type", "flags"));
                details.Add(("RawValue", $"0x{flagsNode.RawValue:X}"));
                foreach (var flag in flagsNode.FlagStates)
                    details.Add(($"  {flag.Name}", flag.Meaning ?? (flag.IsSet ? "set" : "clear")));
                break;

            case DecodedBitfield bitfieldNode:
                details.Add(("Type", "bitfield"));
                details.Add(("RawValue", $"0x{bitfieldNode.RawValue:X}"));
                foreach (var field in bitfieldNode.Fields)
                {
                    var bits = field.BitHigh == field.BitLow
                        ? $"bit {field.BitLow}"
                        : $"bits {field.BitHigh}:{field.BitLow}";
                    details.Add(($"  {field.Name}", $"{field.Value} ({bits})"));
                }
                break;

            case DecodedCompressed compNode:
                details.Add(("Type", "compressed"));
                details.Add(("Algorithm", compNode.Algorithm));
                details.Add(("Compressed", $"{compNode.CompressedSize} bytes"));
                details.Add(("Decompressed", $"{compNode.DecompressedSize} bytes"));
                break;

            case DecodedVirtual virtualNode:
                details.Add(("Type", "virtual"));
                details.Add(("Value", virtualNode.Value?.ToString() ?? ""));
                break;

            case DecodedError errorNode:
                details.Add(("Type", "error"));
                details.Add(("Error", errorNode.ErrorMessage));
                break;
        }

        if (node.Validation is { } v)
            details.Add(("Validation", v.Passed ? $"PASS ({v.Expression})" : $"FAIL ({v.Expression})"));

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
