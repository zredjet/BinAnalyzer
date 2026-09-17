using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Patching;

namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedInteger : DecodedNode
{
    public required long Value { get; init; }
    public string? EnumLabel { get; init; }
    public string? EnumDescription { get; init; }
    public bool? ChecksumValid { get; init; }
    public long? ChecksumExpected { get; init; }
    public string? ChecksumAlgorithm { get; init; }
    public string? StringTableValue { get; init; }

    /// <summary>デコード時の実効エンディアン（書き戻しに使う）。ビットストリーム / 可変長整数では意味を持たない。</summary>
    public Endianness? Endianness { get; init; }

    /// <summary>参照している enum 定義名（編集時の選択肢に使う）。</summary>
    public string? EnumRef { get; init; }

    /// <summary>
    /// チェックサムフィールドの場合、検証時に算出対象とした（ノードと同じデータ空間の）バイト範囲。
    /// 編集範囲との重なりで「再計算が必要か」を判定する。チェックサムでなければ null。
    /// </summary>
    public IReadOnlyList<ByteRange>? ChecksumCoverage { get; init; }
}
