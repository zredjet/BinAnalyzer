using BinAnalyzer.Core.Models;

namespace BinAnalyzer.Engine;

/// <summary>整数系チェックサムの計算（対象バイト列 → 値）。</summary>
public delegate long IntegerChecksum(ReadOnlySpan<byte> data);

/// <summary>ハッシュ系チェックサムの計算（対象バイト列 → ダイジェスト）。</summary>
public delegate byte[] HashChecksum(ReadOnlySpan<byte> data);

/// <summary>
/// チェックサムアルゴリズム名（Core の <see cref="ChecksumAlgorithms"/>）→ 計算の実装（REQ-187）。
/// デコーダはここだけを引く。鍵の集合が Core の整数系 / ハッシュ系の集合と一致することはテストで固定している。
/// </summary>
public static class ChecksumCalculators
{
    public static IReadOnlyDictionary<string, IntegerChecksum> Integer { get; } =
        new Dictionary<string, IntegerChecksum>(StringComparer.OrdinalIgnoreCase)
        {
            [ChecksumAlgorithms.Crc32] = d => Crc32Calculator.Compute(d),
            [ChecksumAlgorithms.Crc16Ccitt] = d => Crc16Calculator.ComputeCcitt(d),
            [ChecksumAlgorithms.Crc16Ibm] = d => Crc16Calculator.ComputeIbm(d),
            [ChecksumAlgorithms.Adler32] = d => Adler32Calculator.Compute(d),
            [ChecksumAlgorithms.Crc8] = d => Crc8Calculator.ComputeSmbus(d),
            [ChecksumAlgorithms.Crc8Maxim] = d => Crc8Calculator.ComputeMaxim(d),
            [ChecksumAlgorithms.Crc8Cdma2000] = d => Crc8Calculator.ComputeCdma2000(d),
            [ChecksumAlgorithms.Crc64Ecma] = d => (long)Crc64Calculator.ComputeEcma(d),
            [ChecksumAlgorithms.XxHash32] = d => XxHashCalculator.ComputeXxHash32(d),
            [ChecksumAlgorithms.XxHash64] = d => (long)XxHashCalculator.ComputeXxHash64(d),
            [ChecksumAlgorithms.Fletcher16] = d => FletcherCalculator.ComputeFletcher16(d),
            [ChecksumAlgorithms.Fletcher32] = d => FletcherCalculator.ComputeFletcher32(d),
        };

    public static IReadOnlyDictionary<string, HashChecksum> Hash { get; } =
        new Dictionary<string, HashChecksum>(StringComparer.OrdinalIgnoreCase)
        {
            [ChecksumAlgorithms.Md5] = HashCalculator.ComputeMd5,
            [ChecksumAlgorithms.Sha1] = HashCalculator.ComputeSha1,
            [ChecksumAlgorithms.Sha256] = HashCalculator.ComputeSha256,
            [ChecksumAlgorithms.Sha384] = HashCalculator.ComputeSha384,
            [ChecksumAlgorithms.Sha512] = HashCalculator.ComputeSha512,
        };
}
