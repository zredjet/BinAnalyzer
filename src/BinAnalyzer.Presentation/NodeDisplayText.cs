using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;

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
            DecodedVirtual v => $"{v.Name}: {VirtualValue(v)}",
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
            DecodedVirtual v => VirtualValue(v),
            DecodedError e => e.ErrorMessage,
            DecodedArray a => $"[{a.Elements.Count} items]",
            _ => "",
        };
    }

    /// <summary>
    /// 型ラベル（インスペクターの「型」行）。<see cref="DecodedNode.DslType"/> があれば DSL の型に基づく正確なラベル
    /// （<c>u32</c> / <c>i16</c> / <c>f32</c> / <c>ascii[4]</c> / <c>uleb128</c>）、無ければサイズと種別からの推定（<c>int32</c> 等）。
    /// </summary>
    public static string TypeLabel(DecodedNode node)
    {
        var dsl = node.DslType;
        return node switch
        {
            DecodedInteger i when i.BitOffset.HasValue => dsl is { } t ? $"{FieldTypeNames.ShortLabel(t)}:{i.Size}bit" : "bits",
            DecodedInteger i when i.ChecksumAlgorithm is not null => $"{IntegerBase(i)} ({i.ChecksumAlgorithm})",
            DecodedInteger i when i.EnumLabel is not null || i.EnumRef is not null => $"{IntegerBase(i)} (enum)",
            DecodedInteger i => IntegerBase(i),
            DecodedFloat f => dsl is { } t ? FieldTypeNames.ShortLabel(t) : f.IsSinglePrecision ? "float32" : "float64",
            DecodedString s => $"{(dsl is { } t ? FieldTypeNames.ToDslName(t) : s.Encoding.ToLowerInvariant())}[{s.Size}]",
            DecodedBytes b when b.ChecksumAlgorithm is not null => $"bytes[{b.Size}] ({b.ChecksumAlgorithm})",
            DecodedBytes b => $"bytes[{b.Size}]",
            DecodedFlags f => dsl is { } t ? $"{FieldTypeNames.ShortLabel(t)} (flags)" : $"flags{f.Size * 8}",
            DecodedBitfield b => $"bitfield{b.Size * 8}",
            DecodedCompressed c => $"bytes ({c.Algorithm})",
            DecodedStruct s => $"struct {s.StructType}",
            DecodedArray a => dsl is { } t ? $"{FieldTypeNames.ShortLabel(t)}[{a.Elements.Count}]" : $"array[{a.Elements.Count}]",
            DecodedVirtual => "virtual",
            DecodedError => "error",
            _ => "unknown",
        };
    }

    private static string IntegerBase(DecodedInteger i)
        => i.DslType is { } t ? FieldTypeNames.ShortLabel(t) : $"int{i.Size * 8}";

    private static string FormatIntegerDisplay(DecodedInteger node)
        => $"{node.Name}: {FormatIntegerValue(node)}";

    /// <summary>virtual の値（<c>= 3</c>）。enum が付いていればラベルを添える（REQ-186）。</summary>
    private static string VirtualValue(DecodedVirtual v) =>
        v.EnumLabel is null ? $"= {v.Value}" : $"= {v.Value} \"{v.EnumLabel}\"";

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
