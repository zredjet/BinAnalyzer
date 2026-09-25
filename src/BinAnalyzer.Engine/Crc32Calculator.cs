namespace BinAnalyzer.Engine;

/// <summary>
/// CRC-32（ISO 3309 / PNG / ZIP準拠）を計算する。
/// 多項式: 0xEDB88320（反転表現）
/// Ogg の CRC-32（<see cref="ComputeOgg"/>）も持つ。
/// </summary>
public static class Crc32Calculator
{
    private static readonly uint[] Table = GenerateTable();
    private static readonly uint[] OggTable = GenerateOggTable();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return ~crc;
    }

    /// <summary>
    /// Ogg のページの CRC-32（RFC 3533・libogg の framing.c）。多項式 0x04C11DB7 を反転せずに使い（上位ビットから）、
    /// 初期値 0・入力と出力の反転無し・最後の XOR 無し。検査値: "123456789" → 0x89A1897F。
    /// </summary>
    public static uint ComputeOgg(ReadOnlySpan<byte> data)
    {
        var crc = 0u;
        foreach (var b in data)
        {
            crc = (crc << 8) ^ OggTable[((crc >> 24) ^ b) & 0xFF];
        }
        return crc;
    }

    private static uint[] GenerateOggTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var crc = i << 24;
            for (var j = 0; j < 8; j++)
            {
                crc = (crc & 0x80000000u) != 0
                    ? (crc << 1) ^ 0x04C11DB7u
                    : crc << 1;
            }
            table[i] = crc;
        }
        return table;
    }

    private static uint[] GenerateTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var crc = i;
            for (var j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0
                    ? 0xEDB88320u ^ (crc >> 1)
                    : crc >> 1;
            }
            table[i] = crc;
        }
        return table;
    }
}
