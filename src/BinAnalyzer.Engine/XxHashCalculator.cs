using System.Buffers.Binary;
using System.IO.Hashing;

namespace BinAnalyzer.Engine;

/// <summary>
/// xxHash32/64 を計算する。System.IO.Hashing の公式実装をラップ。
/// </summary>
public static class XxHashCalculator
{
    public static uint ComputeXxHash32(ReadOnlySpan<byte> data)
    {
        var hash = XxHash32.Hash(data);
        return BinaryPrimitives.ReadUInt32BigEndian(hash);
    }

    public static ulong ComputeXxHash64(ReadOnlySpan<byte> data)
    {
        var hash = XxHash64.Hash(data);
        return BinaryPrimitives.ReadUInt64BigEndian(hash);
    }
}
