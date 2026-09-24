using System.Buffers.Binary;

namespace BinAnalyzer.Integration.Tests;

public static class BmpTestDataGenerator
{
    /// <summary>
    /// 1x1ピクセル、24bit、BI_RGB（無圧縮）の最小BMPを生成する。
    /// ファイルヘッダー(14) + DIBヘッダー(40) + ピクセルデータ(4) = 58バイト
    /// </summary>
    public static byte[] CreateMinimalBmp()
    {
        var data = new byte[58];
        var span = data.AsSpan();

        // ファイルヘッダー (14 bytes)
        data[0] = 0x42; // 'B'
        data[1] = 0x4D; // 'M'
        BinaryPrimitives.WriteUInt32LittleEndian(span[2..], 58);    // file_size
        BinaryPrimitives.WriteUInt16LittleEndian(span[6..], 0);     // reserved1
        BinaryPrimitives.WriteUInt16LittleEndian(span[8..], 0);     // reserved2
        BinaryPrimitives.WriteUInt32LittleEndian(span[10..], 54);   // pixel_offset

        // DIBヘッダー - BITMAPINFOHEADER (40 bytes)
        BinaryPrimitives.WriteUInt32LittleEndian(span[14..], 40);   // header_size
        BinaryPrimitives.WriteInt32LittleEndian(span[18..], 1);     // width
        BinaryPrimitives.WriteInt32LittleEndian(span[22..], 1);     // height
        BinaryPrimitives.WriteUInt16LittleEndian(span[26..], 1);    // planes
        BinaryPrimitives.WriteUInt16LittleEndian(span[28..], 24);   // bits_per_pixel
        BinaryPrimitives.WriteUInt32LittleEndian(span[30..], 0);    // compression = BI_RGB
        BinaryPrimitives.WriteUInt32LittleEndian(span[34..], 4);    // image_size (1行=4bytes with padding)
        BinaryPrimitives.WriteInt32LittleEndian(span[38..], 2835);  // x_pixels_per_meter (~72 DPI)
        BinaryPrimitives.WriteInt32LittleEndian(span[42..], 2835);  // y_pixels_per_meter
        BinaryPrimitives.WriteUInt32LittleEndian(span[46..], 0);    // colors_used
        BinaryPrimitives.WriteUInt32LittleEndian(span[50..], 0);    // colors_important

        // ピクセルデータ (4 bytes: BGR + 1 byte padding)
        data[54] = 0xFF; // Blue
        data[55] = 0x00; // Green
        data[56] = 0x00; // Red
        data[57] = 0x00; // Padding

        return data;
    }

    /// <summary>
    /// BITMAPINFOHEADER + 任意の追加データで BMP を組み立てる。<paramref name="extra"/> はヘッダとピクセルデータの間（マスク・パレット）。
    /// </summary>
    private static byte[] Build(int width, int height, ushort bpp, uint compression, uint colorsUsed, byte[] extra, byte[] pixels)
    {
        var offset = 14 + 40 + extra.Length;
        var data = new byte[offset + pixels.Length];
        var s = data.AsSpan();
        s[0] = (byte)'B'; s[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(s[2..], (uint)data.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(s[10..], (uint)offset);
        BinaryPrimitives.WriteUInt32LittleEndian(s[14..], 40);
        BinaryPrimitives.WriteInt32LittleEndian(s[18..], width);
        BinaryPrimitives.WriteInt32LittleEndian(s[22..], height);
        BinaryPrimitives.WriteUInt16LittleEndian(s[26..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(s[28..], bpp);
        BinaryPrimitives.WriteUInt32LittleEndian(s[30..], compression);
        BinaryPrimitives.WriteUInt32LittleEndian(s[34..], (uint)pixels.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(s[46..], colorsUsed);
        extra.CopyTo(data, 54);
        pixels.CopyTo(data, offset);
        return data;
    }

    /// <summary>1 ビット・colors_used = 0（パレットは 2^1 = 2 色）の 8x1 画像（REQ-188）。</summary>
    public static byte[] CreateMonochromeWithImplicitPalette() =>
        Build(8, 1, 1, 0, 0, [0, 0, 0, 0, 255, 255, 255, 0], [0b1010_0000, 0, 0, 0]);

    /// <summary>16 ビット BI_BITFIELDS（RGB565 のマスク 12 バイト）の 2x1 画像（REQ-188）。</summary>
    public static byte[] CreateRgb565Bitfields() =>
        Build(2, 1, 16, 3, 0, [0x00, 0xF8, 0, 0, 0xE0, 0x07, 0, 0, 0x1F, 0, 0, 0], [0x00, 0xF8, 0x1F, 0x00]);
}
