using BinAnalyzer.Core.Decoded;
using Terminal.Gui;

namespace BinAnalyzer.Tui;

internal sealed class DecodedNodeTreeBuilder : ITreeBuilder<DecodedNode>
{
    public bool SupportsCanExpand => true;

    public bool CanExpand(DecodedNode toExpand)
    {
        return toExpand switch
        {
            DecodedStruct s => s.Children.Any(c => !c.IsPadding),
            DecodedArray a => a.Elements.Count > 0,
            DecodedCompressed c => c.DecodedContent?.Children.Count > 0,
            DecodedBitfield b => b.Fields.Count > 0,
            _ => false,
        };
    }

    public IEnumerable<DecodedNode> GetChildren(DecodedNode forObject)
    {
        return forObject switch
        {
            DecodedStruct s => s.Children.Where(c => !c.IsPadding),
            DecodedArray a => a.Elements,
            DecodedCompressed c when c.DecodedContent is not null => c.DecodedContent.Children,
            _ => [],
        };
    }

    internal static string GetDisplayText(DecodedNode node)
    {
        return node switch
        {
            DecodedStruct s => s.StructType != s.Name
                ? $"{s.Name} \u2192 {s.StructType}"
                : s.Name,
            DecodedArray a => $"{a.Name} [{a.Elements.Count} items]",
            DecodedInteger i => FormatIntegerDisplay(i),
            DecodedFloat f => $"{f.Name}: {f.Value:G}",
            DecodedString s => $"{s.Name}: \"{s.Value}\"",
            DecodedBytes b => $"{b.Name} ({b.Size} bytes)",
            DecodedFlags f => $"{f.Name}: 0x{f.RawValue:X}",
            DecodedBitfield b => $"{b.Name}: 0x{b.RawValue:X}",
            DecodedCompressed c => $"{c.Name} [{c.Algorithm}] ({c.CompressedSize} \u2192 {c.DecompressedSize} bytes)",
            DecodedVirtual v => $"{v.Name}: = {v.Value}",
            DecodedError e => $"\u2717 {e.Name}: {e.ErrorMessage}",
            _ => node.Name,
        };
    }

    private static string FormatIntegerDisplay(DecodedInteger node)
    {
        var text = $"{node.Name}: {node.Value}";
        if (node.EnumLabel is not null)
            text += $" \"{node.EnumLabel}\"";
        if (node.StringTableValue is not null)
            text += $" \u2192 \"{node.StringTableValue}\"";
        return text;
    }
}
