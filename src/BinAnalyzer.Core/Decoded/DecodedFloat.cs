using BinAnalyzer.Core.Models;

namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedFloat : DecodedNode
{
    public required double Value { get; init; }
    public required bool IsSinglePrecision { get; init; }

    /// <summary>デコード時の実効エンディアン（書き戻しに使う）。</summary>
    public Endianness? Endianness { get; init; }
}
