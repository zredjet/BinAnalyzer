using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Diff;

namespace BinAnalyzer.Engine;

public static class DiffEngine
{
    public static DiffResult Compare(DecodedStruct left, DecodedStruct right)
    {
        var entries = new List<DiffEntry>();
        CompareNodes(left, right, "", entries);
        return new DiffResult { Entries = entries };
    }

    private static void CompareNodes(DecodedNode left, DecodedNode right, string path, List<DiffEntry> entries)
    {
        if (left.GetType() != right.GetType())
        {
            entries.Add(new DiffEntry(DiffKind.Changed, path, FormatNodeType(left), FormatNodeType(right)));
            return;
        }

        switch (left)
        {
            case DecodedInteger li when right is DecodedInteger ri:
                CompareInteger(li, ri, path, entries);
                break;
            case DecodedString ls when right is DecodedString rs:
                CompareString(ls, rs, path, entries);
                break;
            case DecodedBytes lb when right is DecodedBytes rb:
                CompareBytes(lb, rb, path, entries);
                break;
            case DecodedFloat lf when right is DecodedFloat rf:
                CompareFloat(lf, rf, path, entries);
                break;
            case DecodedBitfield lbf when right is DecodedBitfield rbf:
                CompareBitfield(lbf, rbf, path, entries);
                break;
            case DecodedStruct lst when right is DecodedStruct rst:
                CompareStruct(lst, rst, path, entries);
                break;
            case DecodedArray la when right is DecodedArray ra:
                CompareArray(la, ra, path, entries);
                break;
            case DecodedCompressed lc when right is DecodedCompressed rc:
                CompareCompressed(lc, rc, path, entries);
                break;
            case DecodedFlags lfl when right is DecodedFlags rfl:
                CompareFlags(lfl, rfl, path, entries);
                break;
            case DecodedVirtual lv when right is DecodedVirtual rv:
                CompareVirtual(lv, rv, path, entries);
                break;
        }
    }

    private static void CompareInteger(DecodedInteger left, DecodedInteger right, string path, List<DiffEntry> entries)
    {
        if (left.Value != right.Value)
        {
            var oldVal = FormatIntegerValue(left);
            var newVal = FormatIntegerValue(right);
            entries.Add(new DiffEntry(DiffKind.Changed, path, oldVal, newVal));
        }
    }

    private static void CompareString(DecodedString left, DecodedString right, string path, List<DiffEntry> entries)
    {
        if (left.Value != right.Value)
        {
            entries.Add(new DiffEntry(DiffKind.Changed, path, $"\"{left.Value}\"", $"\"{right.Value}\""));
        }
    }

    private static void CompareBytes(DecodedBytes left, DecodedBytes right, string path, List<DiffEntry> entries)
    {
        if (!left.RawBytes.Span.SequenceEqual(right.RawBytes.Span))
        {
            entries.Add(new DiffEntry(DiffKind.Changed, path, FormatBytesValue(left.RawBytes), FormatBytesValue(right.RawBytes)));
        }
    }

    private static void CompareFloat(DecodedFloat left, DecodedFloat right, string path, List<DiffEntry> entries)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (left.Value != right.Value)
        {
            entries.Add(new DiffEntry(DiffKind.Changed, path, left.Value.ToString("G"), right.Value.ToString("G")));
        }
    }

    private static void CompareBitfield(DecodedBitfield left, DecodedBitfield right, string path, List<DiffEntry> entries)
    {
        if (left.RawValue != right.RawValue)
        {
            entries.Add(new DiffEntry(DiffKind.Changed, path, $"0x{left.RawValue:X}", $"0x{right.RawValue:X}"));
        }
    }

    private static void CompareStruct(DecodedStruct left, DecodedStruct right, string path, List<DiffEntry> entries)
    {
        var leftByName = new Dictionary<string, DecodedNode>();
        foreach (var child in left.Children)
            leftByName[child.Name] = child;

        var rightByName = new Dictionary<string, DecodedNode>();
        foreach (var child in right.Children)
            rightByName[child.Name] = child;

        // Fields in both
        foreach (var child in left.Children)
        {
            var childPath = string.IsNullOrEmpty(path) ? child.Name : $"{path}.{child.Name}";
            if (rightByName.TryGetValue(child.Name, out var rightChild))
            {
                CompareNodes(child, rightChild, childPath, entries);
            }
            else
            {
                entries.Add(new DiffEntry(DiffKind.Removed, childPath, FormatLeafValue(child), null));
            }
        }

        // Fields only in right
        foreach (var child in right.Children)
        {
            if (!leftByName.ContainsKey(child.Name))
            {
                var childPath = string.IsNullOrEmpty(path) ? child.Name : $"{path}.{child.Name}";
                entries.Add(new DiffEntry(DiffKind.Added, childPath, null, FormatLeafValue(child)));
            }
        }
    }

    private static void CompareArray(DecodedArray left, DecodedArray right, string path, List<DiffEntry> entries)
    {
        var minCount = Math.Min(left.Elements.Count, right.Elements.Count);

        for (var i = 0; i < minCount; i++)
        {
            var elementPath = $"{path}[{i}]";
            CompareNodes(left.Elements[i], right.Elements[i], elementPath, entries);
        }

        // Extra elements in left (removed)
        for (var i = minCount; i < left.Elements.Count; i++)
        {
            var elementPath = $"{path}[{i}]";
            entries.Add(new DiffEntry(DiffKind.Removed, elementPath, FormatLeafValue(left.Elements[i]), null));
        }

        // Extra elements in right (added)
        for (var i = minCount; i < right.Elements.Count; i++)
        {
            var elementPath = $"{path}[{i}]";
            entries.Add(new DiffEntry(DiffKind.Added, elementPath, null, FormatLeafValue(right.Elements[i])));
        }
    }

    private static void CompareFlags(DecodedFlags left, DecodedFlags right, string path, List<DiffEntry> entries)
    {
        if (left.RawValue != right.RawValue)
        {
            entries.Add(new DiffEntry(DiffKind.Changed, path, $"0x{left.RawValue:X}", $"0x{right.RawValue:X}"));
        }
    }

    private static void CompareVirtual(DecodedVirtual left, DecodedVirtual right, string path, List<DiffEntry> entries)
    {
        var leftStr = left.Value?.ToString() ?? "";
        var rightStr = right.Value?.ToString() ?? "";
        if (leftStr != rightStr)
        {
            entries.Add(new DiffEntry(DiffKind.Changed, path, leftStr, rightStr));
        }
    }

    private static void CompareCompressed(DecodedCompressed left, DecodedCompressed right, string path, List<DiffEntry> entries)
    {
        if (left.Algorithm != right.Algorithm)
        {
            entries.Add(new DiffEntry(DiffKind.Changed, $"{path}.algorithm", left.Algorithm, right.Algorithm));
        }

        if (left.DecompressedSize != right.DecompressedSize)
        {
            entries.Add(new DiffEntry(DiffKind.Changed, $"{path}.decompressed_size",
                left.DecompressedSize.ToString(), right.DecompressedSize.ToString()));
        }

        if (left.DecodedContent is not null && right.DecodedContent is not null)
        {
            CompareStruct(left.DecodedContent, right.DecodedContent, path, entries);
        }
        else if (left.RawDecompressed is { } leftRaw && right.RawDecompressed is { } rightRaw)
        {
            if (!leftRaw.Span.SequenceEqual(rightRaw.Span))
            {
                entries.Add(new DiffEntry(DiffKind.Changed, path,
                    FormatBytesValue(leftRaw), FormatBytesValue(rightRaw)));
            }
        }
    }

    private static string FormatIntegerValue(DecodedInteger node)
    {
        var result = node.Value.ToString();
        if (node.EnumLabel is not null)
            result += $" \"{node.EnumLabel}\"";
        return result;
    }

    private static string FormatBytesValue(ReadOnlyMemory<byte> bytes)
    {
        var span = bytes.Span;
        var displayCount = Math.Min(span.Length, 8);
        var parts = new string[displayCount];
        for (var i = 0; i < displayCount; i++)
            parts[i] = span[i].ToString("X2");
        var hex = string.Join(" ", parts);
        if (span.Length > 8)
            hex += " ...";
        return $"[{span.Length} bytes: {hex}]";
    }

    private static string FormatLeafValue(DecodedNode node)
    {
        return node switch
        {
            DecodedInteger i => FormatIntegerValue(i),
            DecodedString s => $"\"{s.Value}\"",
            DecodedBytes b => FormatBytesValue(b.RawBytes),
            DecodedFloat f => f.Value.ToString("G"),
            DecodedBitfield bf => $"0x{bf.RawValue:X}",
            DecodedFlags fl => $"0x{fl.RawValue:X}",
            DecodedVirtual v => v.Value?.ToString() ?? "",
            DecodedStruct st => $"({st.StructType})",
            DecodedArray a => $"[{a.Elements.Count} items]",
            DecodedCompressed c => $"[{c.Algorithm}: {c.CompressedSize}→{c.DecompressedSize} bytes]",
            DecodedError e => $"[ERROR: {e.ErrorMessage}]",
            _ => node.ToString() ?? "",
        };
    }

    private static string FormatNodeType(DecodedNode node)
    {
        return node switch
        {
            DecodedInteger => "integer",
            DecodedString => "string",
            DecodedBytes => "bytes",
            DecodedFloat => "float",
            DecodedBitfield => "bitfield",
            DecodedCompressed c => $"compressed({c.Algorithm})",
            DecodedStruct s => $"struct({s.StructType})",
            DecodedArray => "array",
            DecodedFlags => "flags",
            DecodedVirtual => "virtual",
            DecodedError => "error",
            _ => node.GetType().Name,
        };
    }
}
