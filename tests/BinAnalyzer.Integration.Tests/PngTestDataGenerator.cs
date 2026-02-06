using System.Buffers.Binary;

namespace BinAnalyzer.Integration.Tests;

public static class PngTestDataGenerator
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// IHDRとIENDチャンクのみの最小限の有効なPNGを生成する。
    /// 1x1 truecolor (RGB) ピクセル、ビット深度8。
    /// </summary>
    public static byte[] CreateMinimalPng()
    {
        using var ms = new MemoryStream();

        // PNGシグネチャ
        ms.Write(PngSignature);

        // IHDRチャンク（13バイトのデータ）
        var ihdrData = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdrData.AsSpan(0), 1);  // 幅
        BinaryPrimitives.WriteUInt32BigEndian(ihdrData.AsSpan(4), 1);  // 高さ
        ihdrData[8] = 8;   // ビット深度
        ihdrData[9] = 2;   // カラータイプ: truecolor
        ihdrData[10] = 0;  // 圧縮方式
        ihdrData[11] = 0;  // フィルター方式
        ihdrData[12] = 0;  // インターレース方式
        WriteChunk(ms, "IHDR", ihdrData);

        // IENDチャンク（0バイトのデータ）
        WriteChunk(ms, "IEND", []);

        return ms.ToArray();
    }

    /// <summary>
    /// IHDR、sRGB、IENDチャンクを含むPNGを生成する。
    /// </summary>
    public static byte[] CreatePngWithSrgb()
    {
        using var ms = new MemoryStream();

        ms.Write(PngSignature);

        // IHDR
        var ihdrData = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdrData.AsSpan(0), 2);  // 幅
        BinaryPrimitives.WriteUInt32BigEndian(ihdrData.AsSpan(4), 2);  // 高さ
        ihdrData[8] = 8;
        ihdrData[9] = 2;
        ihdrData[10] = 0;
        ihdrData[11] = 0;
        ihdrData[12] = 0;
        WriteChunk(ms, "IHDR", ihdrData);

        // sRGBチャンク（1バイト: レンダリングインテント）
        WriteChunk(ms, "sRGB", [0x00]); // 知覚的

        // IEND
        WriteChunk(ms, "IEND", []);

        return ms.ToArray();
    }

    private static void WriteChunk(MemoryStream ms, string type, byte[] data)
    {
        var lengthBuf = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthBuf, (uint)data.Length);
        ms.Write(lengthBuf);

        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        ms.Write(typeBytes);

        ms.Write(data);

        // CRC（簡易版: type + data から計算）
        var crc = ComputeCrc(typeBytes, data);
        var crcBuf = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBuf, crc);
        ms.Write(crcBuf);
    }

    private static uint ComputeCrc(byte[] type, byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in type)
            crc = UpdateCrc(crc, b);
        foreach (var b in data)
            crc = UpdateCrc(crc, b);
        return crc ^ 0xFFFFFFFF;
    }

    private static uint UpdateCrc(uint crc, byte b)
    {
        crc ^= b;
        for (var i = 0; i < 8; i++)
        {
            if ((crc & 1) != 0)
                crc = (crc >> 1) ^ 0xEDB88320;
            else
                crc >>= 1;
        }
        return crc;
    }
}
