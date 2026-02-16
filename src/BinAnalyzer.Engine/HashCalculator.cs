using System.Security.Cryptography;

namespace BinAnalyzer.Engine;

/// <summary>
/// ハッシュ系チェックサム（MD5, SHA-1, SHA-256）を計算する。
/// System.Security.Cryptography の HashData 静的メソッドを使用。
/// </summary>
public static class HashCalculator
{
    public static byte[] ComputeMd5(ReadOnlySpan<byte> data) =>
        MD5.HashData(data);

    public static byte[] ComputeSha1(ReadOnlySpan<byte> data) =>
        SHA1.HashData(data);

    public static byte[] ComputeSha256(ReadOnlySpan<byte> data) =>
        SHA256.HashData(data);
}
