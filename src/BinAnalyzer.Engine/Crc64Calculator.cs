namespace BinAnalyzer.Engine;

/// <summary>
/// CRC-64/ECMA-182 を計算する。
/// 多項式: 0x42F0E1EBA9EA3693 (正規表現、非反転)
/// init=0, refIn=false, refOut=false, xorOut=0
/// テストベクター: "123456789" -> 0x6C40DF5F0B497347
/// </summary>
public static class Crc64Calculator
{
    private static readonly ulong[] Table = GenerateTable();

    public static ulong ComputeEcma(ReadOnlySpan<byte> data)
    {
        ulong crc = 0;
        foreach (var b in data)
        {
            crc = Table[((byte)(crc >> 56) ^ b) & 0xFF] ^ (crc << 8);
        }
        return crc;
    }

    private static ulong[] GenerateTable()
    {
        const ulong poly = 0x42F0E1EBA9EA3693uL;
        var table = new ulong[256];
        for (uint i = 0; i < 256; i++)
        {
            var crc = (ulong)i << 56;
            for (int j = 0; j < 8; j++)
            {
                crc = (crc & 0x8000000000000000uL) != 0
                    ? (crc << 1) ^ poly
                    : crc << 1;
            }
            table[i] = crc;
        }
        return table;
    }
}
