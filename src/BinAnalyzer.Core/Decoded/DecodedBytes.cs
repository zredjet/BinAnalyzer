using BinAnalyzer.Core.Patching;

namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedBytes : DecodedNode
{
    public required ReadOnlyMemory<byte> RawBytes { get; init; }
    public bool? ChecksumValid { get; init; }
    public string? ChecksumExpectedHex { get; init; }
    public string? ChecksumAlgorithm { get; init; }

    /// <summary>ハッシュ型チェックサムの場合、検証時に算出対象としたバイト範囲。<see cref="DecodedInteger.ChecksumCoverage"/> と同じ意味。</summary>
    public IReadOnlyList<ByteRange>? ChecksumCoverage { get; init; }
}
