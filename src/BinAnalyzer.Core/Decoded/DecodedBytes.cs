using BinAnalyzer.Core.Patching;

namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedBytes : DecodedNode
{
    public required ReadOnlyMemory<byte> RawBytes { get; init; }

    // チェックサム関連はハッシュ型フィールドだけが使うので補助オブジェクトにまとめる（REQ-180）
    private Extras? _extras;

    public bool? ChecksumValid { get => _extras?.ChecksumValid; init { if (value is not null) (_extras ??= new()).ChecksumValid = value; else if (_extras is not null) _extras.ChecksumValid = null; } }
    public string? ChecksumExpectedHex { get => _extras?.ChecksumExpectedHex; init { if (value is not null) (_extras ??= new()).ChecksumExpectedHex = value; else if (_extras is not null) _extras.ChecksumExpectedHex = null; } }
    public string? ChecksumAlgorithm { get => _extras?.ChecksumAlgorithm; init { if (value is not null) (_extras ??= new()).ChecksumAlgorithm = value; else if (_extras is not null) _extras.ChecksumAlgorithm = null; } }

    /// <summary>ハッシュ型チェックサムの場合、検証時に算出対象としたバイト範囲。<see cref="DecodedInteger.ChecksumCoverage"/> と同じ意味。</summary>
    public IReadOnlyList<ByteRange>? ChecksumCoverage { get => _extras?.ChecksumCoverage; init { if (value is not null) (_extras ??= new()).ChecksumCoverage = value; else if (_extras is not null) _extras.ChecksumCoverage = null; } }

    private sealed class Extras
    {
        public bool? ChecksumValid;
        public string? ChecksumExpectedHex;
        public string? ChecksumAlgorithm;
        public IReadOnlyList<ByteRange>? ChecksumCoverage;
    }
}
