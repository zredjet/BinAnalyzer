namespace BinAnalyzer.Engine;

/// <summary>
/// CRC-16 を計算する。CCITT（多項式 0x1021）と IBM（多項式 0x8005）の2バリアントに対応。
/// </summary>
public static class Crc16Calculator
{
    private static readonly ushort[] CcittTable = GenerateTable(0x1021, reflectInput: false);
    private static readonly ushort[] IbmTable = GenerateTable(0x8005, reflectInput: true);

    /// <summary>
    /// CRC-16/CCITT を計算する。初期値 0xFFFF、入力反転なし、出力反転なし。
    /// テストベクター: "123456789" → 0x29B1
    /// </summary>
    public static ushort ComputeCcitt(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in data)
        {
            crc = (ushort)((crc << 8) ^ CcittTable[((crc >> 8) ^ b) & 0xFF]);
        }
        return crc;
    }

    /// <summary>
    /// CRC-16/IBM を計算する。初期値 0x0000、入力反転あり、出力反転あり。
    /// テストベクター: "123456789" → 0xBB3D
    /// </summary>
    public static ushort ComputeIbm(ReadOnlySpan<byte> data)
    {
        ushort crc = 0x0000;
        foreach (var b in data)
        {
            crc = (ushort)((crc >> 8) ^ IbmTable[(crc ^ b) & 0xFF]);
        }
        return crc;
    }

    private static ushort[] GenerateTable(ushort polynomial, bool reflectInput)
    {
        var table = new ushort[256];
        for (int i = 0; i < 256; i++)
        {
            if (reflectInput)
            {
                ushort crc = (ushort)i;
                for (int j = 0; j < 8; j++)
                {
                    crc = (crc & 1) != 0
                        ? (ushort)((crc >> 1) ^ ReflectPolynomial(polynomial))
                        : (ushort)(crc >> 1);
                }
                table[i] = crc;
            }
            else
            {
                ushort crc = (ushort)(i << 8);
                for (int j = 0; j < 8; j++)
                {
                    crc = (crc & 0x8000) != 0
                        ? (ushort)((crc << 1) ^ polynomial)
                        : (ushort)(crc << 1);
                }
                table[i] = crc;
            }
        }
        return table;
    }

    private static ushort ReflectPolynomial(ushort poly)
    {
        ushort result = 0;
        for (int i = 0; i < 16; i++)
        {
            if ((poly & (1 << i)) != 0)
                result |= (ushort)(1 << (15 - i));
        }
        return result;
    }
}
