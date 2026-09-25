using System.Buffers.Binary;

namespace BinAnalyzer.Engine;

/// <summary>
/// 語の和で作るチェックサムを計算する（REQ-199）。
/// インターネットチェックサム: 16 ビットの語の 1 の補数の和の 1 の補数（IPv4 のヘッダ・ICMP など）
/// 32 ビットの合計: uint32 の語の 2^32 で割った余り（OpenType の表のチェックサム）
/// </summary>
public static class SumChecksumCalculator
{
    /// <summary>
    /// インターネットチェックサム（RFC 1071）。ビッグエンディアンの 16 ビットの語を 1 の補数で足し（桁あふれを下に回す）、その 1 の補数を返す。
    /// 長さが奇数なら最後に 0 のバイトを足す。RFC 1071 の例: 00 01 f2 03 f4 f5 f6 f7 → 和 0xDDF2、チェックサム 0x220D。
    /// </summary>
    public static ushort ComputeInternet(ReadOnlySpan<byte> data)
    {
        ulong sum = 0;
        var i = 0;
        for (; i + 1 < data.Length; i += 2)
            sum += BinaryPrimitives.ReadUInt16BigEndian(data[i..]);
        if (i < data.Length)
            sum += (uint)data[i] << 8;
        while (sum >> 16 != 0)
            sum = (sum & 0xFFFF) + (sum >> 16);
        return (ushort)~sum;
    }

    /// <summary>
    /// ビッグエンディアンの uint32 の語の合計（2^32 で割った余り）。長さが 4 の倍数でなければ後ろを 0 で埋める
    /// （OpenType の CalcTableChecksum）。
    /// </summary>
    public static uint ComputeSum32BigEndian(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        var i = 0;
        for (; i + 3 < data.Length; i += 4)
            sum += BinaryPrimitives.ReadUInt32BigEndian(data[i..]);
        for (var shift = 24; i < data.Length; i++, shift -= 8)
            sum += (uint)data[i] << shift;
        return sum;
    }
}
