namespace BinAnalyzer.Engine;

/// <summary>
/// CRC-32（ISO 3309 / PNG / ZIP準拠）を計算する。
/// 多項式: 0xEDB88320（反転表現）
/// </summary>
public static class Crc32Calculator
{
    private static readonly uint[] Table = GenerateTable();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return ~crc;
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
