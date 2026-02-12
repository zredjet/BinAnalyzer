using System.Text;
using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Output;

public sealed class DiffTreeOutputFormatter
{
    private readonly bool _useColor;
    private bool _hasDifferences;

    public DiffTreeOutputFormatter(ColorMode mode = ColorMode.Never)
    {
        _useColor = mode switch
        {
            ColorMode.Always => true,
            ColorMode.Never => false,
            ColorMode.Auto => !Console.IsOutputRedirected,
            _ => false,
        };
    }

    public bool HasDifferences => _hasDifferences;

    private string C(string text, string color)
        => _useColor ? $"{color}{text}{AnsiColors.Reset}" : text;

    public string Format(DecodedStruct left, DecodedStruct right)
    {
        _hasDifferences = false;
        var sb = new StringBuilder();
        FormatStruct(sb, left, right, "");
        return sb.ToString();
    }

    private void FormatStruct(StringBuilder sb, DecodedStruct left, DecodedStruct right, string indent)
    {
        var leftByName = new Dictionary<string, DecodedNode>();
        foreach (var child in left.Children)
            leftByName[child.Name] = child;

        var rightByName = new Dictionary<string, DecodedNode>();
        foreach (var child in right.Children)
            rightByName[child.Name] = child;

        // Process fields in left order, then added fields from right
        foreach (var lChild in left.Children)
        {
            if (rightByName.TryGetValue(lChild.Name, out var rChild))
            {
                FormatNodePair(sb, lChild, rChild, indent);
            }
            else
            {
                // Removed
                _hasDifferences = true;
                FormatRemovedNode(sb, lChild, indent);
            }
        }

        // Fields only in right (added)
        foreach (var rChild in right.Children)
        {
            if (!leftByName.ContainsKey(rChild.Name))
            {
                _hasDifferences = true;
                FormatAddedNode(sb, rChild, indent);
            }
        }
    }

    private void FormatNodePair(StringBuilder sb, DecodedNode left, DecodedNode right, string indent)
    {
        if (left.GetType() != right.GetType())
        {
            _hasDifferences = true;
            sb.AppendLine(C($"{indent}{left.Name}  {FormatLeafDisplay(left)} → {FormatLeafDisplay(right)}", AnsiColors.Yellow));
            return;
        }

        switch (left)
        {
            case DecodedStruct ls when right is DecodedStruct rs:
                if (AllChildrenIdentical(ls, rs))
                {
                    sb.AppendLine(C($"{indent}{ls.Name}  (同一)", AnsiColors.Dim));
                }
                else
                {
                    sb.AppendLine($"{indent}{ls.Name}");
                    FormatStruct(sb, ls, rs, indent + "  ");
                }
                break;

            case DecodedArray la when right is DecodedArray ra:
                if (AllElementsIdentical(la, ra))
                {
                    sb.AppendLine(C($"{indent}{la.Name}  (同一)", AnsiColors.Dim));
                }
                else
                {
                    sb.AppendLine($"{indent}{la.Name}");
                    FormatArray(sb, la, ra, indent + "  ");
                }
                break;

            case DecodedCompressed lc when right is DecodedCompressed rc:
                FormatCompressedPair(sb, lc, rc, indent);
                break;

            default:
                // Leaf comparison
                FormatLeafPair(sb, left, right, indent);
                break;
        }
    }

    private void FormatLeafPair(StringBuilder sb, DecodedNode left, DecodedNode right, string indent)
    {
        var leftVal = FormatLeafDisplay(left);
        var rightVal = FormatLeafDisplay(right);

        if (leftVal == rightVal)
        {
            sb.AppendLine(C($"{indent}{left.Name}  (同一)", AnsiColors.Dim));
        }
        else
        {
            _hasDifferences = true;
            sb.AppendLine(C($"{indent}{left.Name}  {leftVal} → {rightVal}", AnsiColors.Yellow));
        }
    }

    private void FormatArray(StringBuilder sb, DecodedArray left, DecodedArray right, string indent)
    {
        if (left.DiffKey is { Count: > 0 } diffKeys && CanUseKeyedFormat(left, right, diffKeys))
        {
            FormatArrayByKey(sb, left, right, diffKeys, indent);
            return;
        }

        var minCount = Math.Min(left.Elements.Count, right.Elements.Count);

        for (var i = 0; i < minCount; i++)
        {
            var lElem = left.Elements[i];
            var rElem = right.Elements[i];

            if (lElem is DecodedStruct ls && rElem is DecodedStruct rs)
            {
                if (AllChildrenIdentical(ls, rs))
                {
                    sb.AppendLine(C($"{indent}[{i}]  (同一)", AnsiColors.Dim));
                }
                else
                {
                    sb.AppendLine($"{indent}[{i}]");
                    FormatStruct(sb, ls, rs, indent + "  ");
                }
            }
            else
            {
                FormatArrayElementPair(sb, lElem, rElem, i, indent);
            }
        }

        // Extra elements in left (removed)
        for (var i = minCount; i < left.Elements.Count; i++)
        {
            _hasDifferences = true;
            sb.AppendLine(C($"{indent}- [{i}]: {FormatLeafDisplay(left.Elements[i])}", AnsiColors.Red));
        }

        // Extra elements in right (added)
        for (var i = minCount; i < right.Elements.Count; i++)
        {
            _hasDifferences = true;
            sb.AppendLine(C($"{indent}+ [{i}]: {FormatLeafDisplay(right.Elements[i])}", AnsiColors.Green));
        }
    }

    private void FormatArrayByKey(StringBuilder sb, DecodedArray left, DecodedArray right, IReadOnlyList<string> keyFields, string indent)
    {
        var rightByKey = new Dictionary<string, DecodedNode>();
        foreach (var elem in right.Elements)
        {
            var key = ExtractCompositeKey(elem, keyFields);
            if (key is not null)
                rightByKey.TryAdd(key, elem);
        }

        var leftKeys = new HashSet<string>();
        foreach (var elem in left.Elements)
        {
            var key = ExtractCompositeKey(elem, keyFields)!;
            leftKeys.Add(key);
            var label = BuildKeyedLabel(keyFields, elem);

            if (rightByKey.TryGetValue(key, out var rElem))
            {
                if (elem is DecodedStruct ls && rElem is DecodedStruct rs)
                {
                    if (AllChildrenIdentical(ls, rs))
                    {
                        sb.AppendLine(C($"{indent}{label}  (同一)", AnsiColors.Dim));
                    }
                    else
                    {
                        sb.AppendLine($"{indent}{label}");
                        FormatStruct(sb, ls, rs, indent + "  ");
                    }
                }
                else
                {
                    var leftVal = FormatLeafDisplay(elem);
                    var rightVal = FormatLeafDisplay(rElem);
                    if (leftVal == rightVal)
                    {
                        sb.AppendLine(C($"{indent}{label}  (同一)", AnsiColors.Dim));
                    }
                    else
                    {
                        _hasDifferences = true;
                        sb.AppendLine(C($"{indent}{label}  {leftVal} → {rightVal}", AnsiColors.Yellow));
                    }
                }
            }
            else
            {
                _hasDifferences = true;
                sb.AppendLine(C($"{indent}- {label}: {FormatLeafDisplay(elem)}", AnsiColors.Red));
            }
        }

        // Elements only in right (added, in right's order)
        foreach (var elem in right.Elements)
        {
            var key = ExtractCompositeKey(elem, keyFields)!;
            if (!leftKeys.Contains(key))
            {
                _hasDifferences = true;
                var label = BuildKeyedLabel(keyFields, elem);
                sb.AppendLine(C($"{indent}+ {label}: {FormatLeafDisplay(elem)}", AnsiColors.Green));
            }
        }
    }

    private static bool CanUseKeyedFormat(DecodedArray left, DecodedArray right, IReadOnlyList<string> keyFields)
    {
        foreach (var elem in left.Elements)
        {
            if (ExtractCompositeKey(elem, keyFields) is null)
                return false;
        }
        foreach (var elem in right.Elements)
        {
            if (ExtractCompositeKey(elem, keyFields) is null)
                return false;
        }
        return true;
    }

    private static string? ExtractCompositeKey(DecodedNode element, IReadOnlyList<string> keyFields)
    {
        if (element is not DecodedStruct st)
            return null;

        if (keyFields.Count == 1)
            return ExtractSingleFieldValue(st, keyFields[0]);

        var parts = new string[keyFields.Count];
        for (var i = 0; i < keyFields.Count; i++)
        {
            var val = ExtractSingleFieldValue(st, keyFields[i]);
            if (val is null) return null;
            parts[i] = val;
        }
        return string.Join("\0", parts);
    }

    private static string? ExtractSingleFieldValue(DecodedStruct st, string fieldName)
    {
        foreach (var child in st.Children)
        {
            if (child.Name == fieldName)
            {
                return child switch
                {
                    DecodedInteger i => i.EnumLabel ?? i.Value.ToString(),
                    DecodedString s => s.Value,
                    _ => null,
                };
            }
        }
        return null;
    }

    private static string BuildKeyedLabel(IReadOnlyList<string> keyFields, DecodedNode element)
    {
        if (element is not DecodedStruct st)
            return "[]";

        if (keyFields.Count == 1)
        {
            var val = ExtractSingleFieldValue(st, keyFields[0]);
            return $"[{keyFields[0]}={val}]";
        }

        var pairs = keyFields.Select(f => $"{f}={ExtractSingleFieldValue(st, f)}");
        return $"[{string.Join(",", pairs)}]";
    }

    private void FormatArrayElementPair(StringBuilder sb, DecodedNode left, DecodedNode right, int index, string indent)
    {
        var leftVal = FormatLeafDisplay(left);
        var rightVal = FormatLeafDisplay(right);

        if (leftVal == rightVal)
        {
            sb.AppendLine(C($"{indent}[{index}]  (同一)", AnsiColors.Dim));
        }
        else
        {
            _hasDifferences = true;
            sb.AppendLine(C($"{indent}[{index}]  {leftVal} → {rightVal}", AnsiColors.Yellow));
        }
    }

    private void FormatCompressedPair(StringBuilder sb, DecodedCompressed left, DecodedCompressed right, string indent)
    {
        if (left.DecodedContent is not null && right.DecodedContent is not null)
        {
            if (AllChildrenIdentical(left.DecodedContent, right.DecodedContent)
                && left.Algorithm == right.Algorithm
                && left.DecompressedSize == right.DecompressedSize)
            {
                sb.AppendLine(C($"{indent}{left.Name}  (同一)", AnsiColors.Dim));
            }
            else
            {
                sb.AppendLine($"{indent}{left.Name}");
                FormatStruct(sb, left.DecodedContent, right.DecodedContent, indent + "  ");
            }
        }
        else
        {
            var leftVal = FormatLeafDisplay(left);
            var rightVal = FormatLeafDisplay(right);
            if (leftVal == rightVal)
            {
                sb.AppendLine(C($"{indent}{left.Name}  (同一)", AnsiColors.Dim));
            }
            else
            {
                _hasDifferences = true;
                sb.AppendLine(C($"{indent}{left.Name}  {leftVal} → {rightVal}", AnsiColors.Yellow));
            }
        }
    }

    private void FormatAddedNode(StringBuilder sb, DecodedNode node, string indent)
    {
        if (node is DecodedStruct)
        {
            sb.AppendLine(C($"{indent}+ {node.Name} (struct)", AnsiColors.Green));
        }
        else
        {
            sb.AppendLine(C($"{indent}+ {node.Name}: {FormatLeafDisplay(node)}", AnsiColors.Green));
        }
    }

    private void FormatRemovedNode(StringBuilder sb, DecodedNode node, string indent)
    {
        if (node is DecodedStruct)
        {
            sb.AppendLine(C($"{indent}- {node.Name} (struct)", AnsiColors.Red));
        }
        else
        {
            sb.AppendLine(C($"{indent}- {node.Name}: {FormatLeafDisplay(node)}", AnsiColors.Red));
        }
    }

    private bool AllChildrenIdentical(DecodedStruct left, DecodedStruct right)
    {
        if (left.Children.Count != right.Children.Count)
            return false;

        var rightByName = new Dictionary<string, DecodedNode>();
        foreach (var child in right.Children)
            rightByName[child.Name] = child;

        foreach (var lChild in left.Children)
        {
            if (!rightByName.TryGetValue(lChild.Name, out var rChild))
                return false;
            if (!NodesIdentical(lChild, rChild))
                return false;
        }

        return true;
    }

    private bool AllElementsIdentical(DecodedArray left, DecodedArray right)
    {
        if (left.Elements.Count != right.Elements.Count)
            return false;

        if (left.DiffKey is { Count: > 0 } diffKeys && CanUseKeyedFormat(left, right, diffKeys))
        {
            var rightByKey = new Dictionary<string, DecodedNode>();
            foreach (var elem in right.Elements)
            {
                var key = ExtractCompositeKey(elem, diffKeys);
                if (key is not null)
                    rightByKey.TryAdd(key, elem);
            }

            foreach (var elem in left.Elements)
            {
                var key = ExtractCompositeKey(elem, diffKeys);
                if (key is null || !rightByKey.TryGetValue(key, out var rElem))
                    return false;
                if (!NodesIdentical(elem, rElem))
                    return false;
            }
            return true;
        }

        for (var i = 0; i < left.Elements.Count; i++)
        {
            if (!NodesIdentical(left.Elements[i], right.Elements[i]))
                return false;
        }

        return true;
    }

    private bool NodesIdentical(DecodedNode left, DecodedNode right)
    {
        if (left.GetType() != right.GetType())
            return false;

        return (left, right) switch
        {
            (DecodedStruct ls, DecodedStruct rs) => AllChildrenIdentical(ls, rs),
            (DecodedArray la, DecodedArray ra) => AllElementsIdentical(la, ra),
            (DecodedCompressed lc, DecodedCompressed rc) => CompressedIdentical(lc, rc),
            _ => FormatLeafDisplay(left) == FormatLeafDisplay(right),
        };
    }

    private bool CompressedIdentical(DecodedCompressed left, DecodedCompressed right)
    {
        if (left.Algorithm != right.Algorithm || left.DecompressedSize != right.DecompressedSize)
            return false;
        if (left.DecodedContent is not null && right.DecodedContent is not null)
            return AllChildrenIdentical(left.DecodedContent, right.DecodedContent);
        return FormatLeafDisplay(left) == FormatLeafDisplay(right);
    }

    private static string FormatLeafDisplay(DecodedNode node)
    {
        return node switch
        {
            DecodedInteger i => FormatIntegerDisplay(i),
            DecodedString s => $"\"{s.Value}\"",
            DecodedBytes b => FormatBytesDisplay(b.RawBytes),
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

    private static string FormatIntegerDisplay(DecodedInteger node)
    {
        var result = node.Value.ToString();
        if (node.EnumLabel is not null)
            result += $" \"{node.EnumLabel}\"";
        return result;
    }

    private static string FormatBytesDisplay(ReadOnlyMemory<byte> bytes)
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
}
