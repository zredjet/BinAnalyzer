using System.Text.RegularExpressions;
using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Presentation;

/// <summary>フィールドの「意味の種別」。ヘックス・ツリー・構造マップの色分けに使う。</summary>
public enum FieldKind
{
    Root,
    Struct,
    Array,
    /// <summary>マジックナンバー・シグネチャ（expected / validate 付きの bytes・文字列）</summary>
    Magic,
    /// <summary>長さ・サイズ・個数を表す整数</summary>
    Len,
    /// <summary>チャンク種別などの短い識別子文字列</summary>
    Tag,
    Num,
    Str,
    Bytes,
    /// <summary>チェックサム</summary>
    Crc,
    /// <summary>圧縮データ</summary>
    Zip,
    Pad,
    Flags,
    Bitfield,
    Virtual,
    Error,
}

public static partial class FieldKindMapper
{
    [GeneratedRegex(@"^(len|length|size|count|num|n)$|(_|^)(len|length|size|count)$|^(num|n)_", RegexOptions.IgnoreCase)]
    private static partial Regex LengthName();

    [GeneratedRegex(@"^(magic|signature|sig|header_magic)$", RegexOptions.IgnoreCase)]
    private static partial Regex MagicName();

    [GeneratedRegex(@"(^|_)(tag|type|fourcc|id|code|kind|chunk_type)$", RegexOptions.IgnoreCase)]
    private static partial Regex TagName();

    /// <summary>
    /// ノードの種別を判定する。優先順位: padding > error > compressed > checksum > magic > tag > len > 型既定。
    /// </summary>
    public static FieldKind Map(DecodedNode node, DecodedNode? parent)
    {
        if (parent is null && node is DecodedStruct)
            return FieldKind.Root;
        if (node.IsPadding)
            return FieldKind.Pad;

        return node switch
        {
            DecodedError => FieldKind.Error,
            DecodedCompressed => FieldKind.Zip,
            DecodedInteger { ChecksumAlgorithm: not null } => FieldKind.Crc,
            DecodedBytes { ChecksumAlgorithm: not null } => FieldKind.Crc,
            DecodedBytes { ValidationPassed: not null } => FieldKind.Magic,
            DecodedBytes b when MagicName().IsMatch(b.Name) => FieldKind.Magic,
            DecodedString s when s.Validation is not null || MagicName().IsMatch(s.Name) => FieldKind.Magic,
            DecodedString { Flags: not null } => FieldKind.Tag,
            DecodedString s when s.Size <= 8 && TagName().IsMatch(s.Name) => FieldKind.Tag,
            DecodedInteger i when LengthName().IsMatch(i.Name) => FieldKind.Len,
            DecodedInteger => FieldKind.Num,
            DecodedFloat => FieldKind.Num,
            DecodedString => FieldKind.Str,
            DecodedBytes => FieldKind.Bytes,
            DecodedFlags => FieldKind.Flags,
            DecodedBitfield => FieldKind.Bitfield,
            DecodedVirtual => FieldKind.Virtual,
            DecodedStruct => FieldKind.Struct,
            DecodedArray => FieldKind.Array,
            _ => FieldKind.Bytes,
        };
    }

    /// <summary>CSS クラス名（例: <c>k-num</c>）。</summary>
    public static string CssClass(FieldKind kind) => "k-" + kind.ToString().ToLowerInvariant();
}
