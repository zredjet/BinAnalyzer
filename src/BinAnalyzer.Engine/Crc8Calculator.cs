namespace BinAnalyzer.Engine;

/// <summary>
/// CRC-8 を計算する。SMBus, Maxim (1-Wire), CDMA2000 の3バリアントに対応。
/// </summary>
public static class Crc8Calculator
{
    // CRC-8/SMBus: poly=0x07, init=0x00, refIn=false, refOut=false
    private static readonly byte[] SmbusTable = GenerateTable(0x07, reflect: false);

    // CRC-8/Maxim (1-Wire): poly=0x31, init=0x00, refIn=true, refOut=true
    private static readonly byte[] MaximTable = GenerateTable(0x31, reflect: true);

    // CRC-8/CDMA2000: poly=0x9B, init=0xFF, refIn=false, refOut=false
    private static readonly byte[] Cdma2000Table = GenerateTable(0x9B, reflect: false);

    /// <summary>
    /// CRC-8/SMBus を計算する。
    /// テストベクター: "123456789" -> 0xF4
    /// </summary>
    public static byte ComputeSmbus(ReadOnlySpan<byte> data)
    {
        byte crc = 0x00;
        foreach (var b in data)
            crc = SmbusTable[crc ^ b];
        return crc;
    }

    /// <summary>
    /// CRC-8/Maxim (1-Wire) を計算する。
    /// テストベクター: "123456789" -> 0xA1
    /// </summary>
    public static byte ComputeMaxim(ReadOnlySpan<byte> data)
    {
        byte crc = 0x00;
        foreach (var b in data)
            crc = MaximTable[crc ^ b];
        return crc;
    }

    /// <summary>
    /// CRC-8/CDMA2000 を計算する。
    /// テストベクター: "123456789" -> 0xDA
    /// </summary>
    public static byte ComputeCdma2000(ReadOnlySpan<byte> data)
    {
        byte crc = 0xFF;
        foreach (var b in data)
            crc = Cdma2000Table[crc ^ b];
        return crc;
    }

    private static byte[] GenerateTable(byte polynomial, bool reflect)
    {
        var table = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            if (reflect)
            {
                byte crc = (byte)i;
                for (int j = 0; j < 8; j++)
                {
                    crc = (crc & 1) != 0
                        ? (byte)((crc >> 1) ^ ReflectByte(polynomial))
                        : (byte)(crc >> 1);
                }
                table[i] = crc;
            }
            else
            {
                byte crc = (byte)i;
                for (int j = 0; j < 8; j++)
                {
                    crc = (crc & 0x80) != 0
                        ? (byte)((crc << 1) ^ polynomial)
                        : (byte)(crc << 1);
                }
                table[i] = crc;
            }
        }
        return table;
    }

    private static byte ReflectByte(byte value)
    {
        byte result = 0;
        for (int i = 0; i < 8; i++)
        {
            if ((value & (1 << i)) != 0)
                result |= (byte)(1 << (7 - i));
        }
        return result;
    }
}
