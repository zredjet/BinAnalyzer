using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Presentation;

/// <summary>ノード 1 行分の表示文字列。TUI ツリーと GUI ツリーで共通。</summary>
public static class NodeDisplayText
{
    public static string For(DecodedNode node)
    {
        return node switch
        {
            DecodedStruct s => s.StructType != s.Name
                ? $"{s.Name} → {s.StructType}"
                : s.Name,
            DecodedArray a => $"{a.Name} [{a.Elements.Count} items]",
            DecodedInteger i => FormatIntegerDisplay(i),
            DecodedFloat f => $"{f.Name}: {f.Value:G}",
            DecodedString s => $"{s.Name}: \"{s.Value}\"",
            DecodedBytes b => $"{b.Name} ({b.Size} bytes)",
            DecodedFlags f => $"{f.Name}: 0x{f.RawValue:X}",
            DecodedBitfield b => $"{b.Name}: 0x{b.RawValue:X}",
            DecodedCompressed c => $"{c.Name} [{c.Algorithm}] ({c.CompressedSize} → {c.DecompressedSize} bytes)",
            DecodedVirtual v => $"{v.Name}: = {v.Value}",
            DecodedError e => $"✗ {e.Name}: {e.ErrorMessage}",
            _ => node.Name,
        };
    }

    /// <summary>名前を除いた値部分（ヘックス行のゴースト注釈等で使う）。値を持たないノードは空文字。</summary>
    public static string ValueOnly(DecodedNode node)
    {
        return node switch
        {
            DecodedInteger i => FormatIntegerValue(i),
            DecodedFloat f => f.Value.ToString("G"),
            DecodedString s => $"\"{s.Value}\"",
            DecodedFlags f => $"0x{f.RawValue:X}",
            DecodedBitfield b => $"0x{b.RawValue:X}",
            DecodedCompressed c => $"{c.Algorithm} {c.CompressedSize} B → {c.DecompressedSize} B",
            DecodedVirtual v => $"= {v.Value}",
            DecodedError e => e.ErrorMessage,
            DecodedArray a => $"[{a.Elements.Count} items]",
            _ => "",
        };
    }

    /// <summary>
    /// 型ラベル（インスペクターの「型」行）。デコード結果は符号や DSL 型名を保持しないため、
    /// サイズと種別から推定した簡潔なラベルを返す。
    /// </summary>
    public static string TypeLabel(DecodedNode node)
    {
        return node switch
        {
            DecodedInteger i when i.ChecksumAlgorithm is not null => $"int{i.Size * 8} ({i.ChecksumAlgorithm})",
            DecodedInteger i when i.EnumLabel is not null => $"int{i.Size * 8} (enum)",
            DecodedInteger i when i.BitOffset.HasValue => "bits",
            DecodedInteger i => $"int{i.Size * 8}",
            DecodedFloat f => f.IsSinglePrecision ? "float32" : "float64",
            DecodedString s => $"{s.Encoding.ToLowerInvariant()}[{s.Size}]",
            DecodedBytes b when b.ChecksumAlgorithm is not null => $"bytes[{b.Size}] ({b.ChecksumAlgorithm})",
            DecodedBytes b => $"bytes[{b.Size}]",
            DecodedFlags f => $"flags{f.Size * 8}",
            DecodedBitfield b => $"bitfield{b.Size * 8}",
            DecodedCompressed c => $"bytes ({c.Algorithm})",
            DecodedStruct s => $"struct {s.StructType}",
            DecodedArray a => $"array[{a.Elements.Count}]",
            DecodedVirtual => "virtual",
            DecodedError => "error",
            _ => "unknown",
        };
    }

    private static string FormatIntegerDisplay(DecodedInteger node)
        => $"{node.Name}: {FormatIntegerValue(node)}";

    private static string FormatIntegerValue(DecodedInteger node)
    {
        var text = node.Value.ToString();
        if (node.EnumLabel is not null)
            text += $" \"{node.EnumLabel}\"";
        if (node.StringTableValue is not null)
            text += $" → \"{node.StringTableValue}\"";
        return text;
    }
}
