using System.Buffers.Binary;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class TiffTestDataGenerator
{
    /// <summary>
    /// 最小TIFFファイル(リトルエンディアン): header(8B) + IFD(2B entry_count + 12B entry + 4B next_ifd_offset) = 26バイト
    /// "II" byte order, magic 42, 1 IFD entry (ImageWidth=1)
    /// </summary>
    public static byte[] CreateMinimalTiff()
    {
        var data = new byte[26];
        var span = data.AsSpan();
        var pos = 0;

        // === tiff_header ===
        // byte_order: "II" (little-endian)
        Encoding.ASCII.GetBytes("II").CopyTo(span[pos..]); pos += 2;

        // magic: 42
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 42); pos += 2;

        // ifd_offset: 8 (IFD starts right after header)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 8); pos += 4;

        // === IFD ===
        // entry_count: 1
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 1); pos += 2;

        // IFD entry: ImageWidth (tag=256, type=SHORT(3), count=1, value=64)
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 256); pos += 2;  // tag: ImageWidth
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 3);   pos += 2;  // field_type: SHORT
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1);   pos += 4;  // count: 1
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 64);  pos += 4;  // value: 64

        // next_ifd_offset: 0 (no more IFDs)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0);

        return data;
    }
}
