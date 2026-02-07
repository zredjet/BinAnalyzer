using System.Buffers.Binary;

namespace BinAnalyzer.Integration.Tests;

public static class IcoTestDataGenerator
{
    /// <summary>
    /// 最小ICOファイル: header(6B) + 1 entry(16B) + BMP DIB image data(40B) = 62バイト
    /// 1x1ピクセル、32bpp、BMP DIBイメージ
    /// </summary>
    public static byte[] CreateMinimalIco()
    {
        var data = new byte[62];
        var span = data.AsSpan();
        var pos = 0;

        // === ICO header ===
        // reserved: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;

        // type: 1 (ICO)
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 1); pos += 2;

        // count: 1
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 1); pos += 2;

        // === ICO entry ===
        // width: 1
        data[pos] = 1; pos += 1;

        // height: 1
        data[pos] = 1; pos += 1;

        // color_count: 0
        data[pos] = 0; pos += 1;

        // reserved: 0
        data[pos] = 0; pos += 1;

        // planes: 1
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 1); pos += 2;

        // bpp: 32
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 32); pos += 2;

        // bytes_in_res: 40 (size of BMP DIB data)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 40); pos += 4;

        // image_offset: 22 (6 header + 16 entry)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 22); pos += 4;

        // === BMP DIB image data at offset 22 ===
        // image_magic will be read as uint32 big-endian from offset 22
        // First 4 bytes of BITMAPINFOHEADER: DIB header size = 40 (0x00000028 LE)
        // As big-endian uint32: 0x28000000
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 40); pos += 4; // biSize

        // biWidth: 1
        BinaryPrimitives.WriteInt32LittleEndian(span[pos..], 1); pos += 4;

        // biHeight: 2 (ICO doubles height for XOR+AND masks)
        BinaryPrimitives.WriteInt32LittleEndian(span[pos..], 2); pos += 4;

        // biPlanes: 1
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 1); pos += 2;

        // biBitCount: 32
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 32); pos += 2;

        // biCompression: 0 (BI_RGB)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;

        // biSizeImage: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;

        // biXPelsPerMeter: 0
        BinaryPrimitives.WriteInt32LittleEndian(span[pos..], 0); pos += 4;

        // biYPelsPerMeter: 0
        BinaryPrimitives.WriteInt32LittleEndian(span[pos..], 0); pos += 4;

        // biClrUsed: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;

        // biClrImportant: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0);

        return data;
    }
}
