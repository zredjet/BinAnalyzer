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

    /// <summary>
    /// PNG 第 3 版のチャンクと APNG を含む 2 フレームの RGB 画像（2x1）（REQ-188）。
    /// IHDR, cICP, mDCV, cLLI, iCCP, sBIT, PLTE（推奨パレット）, bKGD, hIST, sPLT, eXIf, tIME, acTL, fcTL, IDAT, fcTL, fdAT, IEND。
    /// </summary>
    public static byte[] CreateThirdEditionApng()
    {
        var ms = new MemoryStream();
        ms.Write(PngSignature);
        WriteChunk(ms, "IHDR", [0, 0, 0, 2, 0, 0, 0, 1, 8, 2, 0, 0, 0]);    // 2x1, 8 bit, RGB
        WriteChunk(ms, "cICP", [9, 16, 0, 1]);                                // BT.2020, PQ, RGB, full range
        WriteChunk(ms, "mDCV", Be(
            (2, 35400), (2, 14600), (2, 8500), (2, 39850), (2, 6550), (2, 2300),  // 原色（0.00002 単位）
            (2, 15635), (2, 16450),                                               // 白色点
            (4, 10_000_000), (4, 1)));                                            // 輝度（0.0001 cd/m²）
        WriteChunk(ms, "cLLI", Be((4, 10_000_000), (4, 4_000_000)));
        WriteChunk(ms, "iCCP", [.. "icc"u8, 0, 0, .. Zlib("not a real ICC profile"u8.ToArray())]);
        WriteChunk(ms, "sBIT", [5, 6, 5]);
        WriteChunk(ms, "PLTE", [255, 0, 0, 0, 0, 255]);
        WriteChunk(ms, "bKGD", Be((2, 1), (2, 2), (2, 3)));
        WriteChunk(ms, "hIST", Be((2, 10), (2, 20)));
        WriteChunk(ms, "sPLT", [.. "suggested"u8, 0, 8, 255, 0, 0, 255, 0, 5, 0, 0, 255, 128, 0, 1]);
        WriteChunk(ms, "eXIf", [.. "MM"u8, 0, 42, 0, 0, 0, 8, 0, 0]);
        WriteChunk(ms, "tIME", [0x07, 0xEA, 9, 24, 12, 34, 56]);             // 2026-09-24 12:34:56
        WriteChunk(ms, "acTL", Be((4, 2), (4, 0)));                           // 2 フレーム、無限ループ
        WriteChunk(ms, "fcTL", FrameControl(sequence: 0, delayNum: 1, delayDen: 10, dispose: 0, blend: 0));
        var frame0 = Zlib([0, 255, 0, 0, 255, 0, 0]);                         // フィルタ 0 + 赤 2 画素
        WriteChunk(ms, "IDAT", frame0);
        WriteChunk(ms, "fcTL", FrameControl(sequence: 1, delayNum: 1, delayDen: 5, dispose: 1, blend: 1));
        WriteChunk(ms, "fdAT", [.. Be((4, 2)), .. Zlib([0, 0, 0, 255, 0, 0, 255])]);  // 青 2 画素
        WriteChunk(ms, "IEND", []);
        return ms.ToArray();
    }

    private static byte[] FrameControl(uint sequence, ushort delayNum, ushort delayDen, byte dispose, byte blend) =>
        [.. Be((4, sequence), (4, 2), (4, 1), (4, 0), (4, 0), (2, delayNum), (2, delayDen)), dispose, blend];

    /// <summary>ビッグエンディアンの (バイト数, 値) の並び。</summary>
    private static byte[] Be(params (int Size, long Value)[] values)
    {
        var result = new List<byte>();
        foreach (var (size, value) in values)
            for (var i = size - 1; i >= 0; i--)
                result.Add((byte)(value >> (8 * i)));
        return [.. result];
    }

    private static byte[] Zlib(byte[] data)
    {
        var output = new MemoryStream();
        using (var z = new System.IO.Compression.ZLibStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            z.Write(data);
        return output.ToArray();
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
