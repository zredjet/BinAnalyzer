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

    /// <summary>
    /// 最小TIFFファイル(ビッグエンディアン): header(8B) + IFD(2B entry_count + 12B entry + 4B next_ifd_offset) = 26バイト
    /// "MM" byte order, magic 42, 1 IFD entry (ImageWidth=1)
    /// </summary>
    public static byte[] CreateBigEndianTiff()
    {
        var data = new byte[26];
        var span = data.AsSpan();
        var pos = 0;

        // === byte_order ===
        // byte_order: "MM" (big-endian)
        Encoding.ASCII.GetBytes("MM").CopyTo(span[pos..]); pos += 2;

        // magic: 42
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 42); pos += 2;

        // ifd_offset: 8 (IFD starts right after header)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 8); pos += 4;

        // === IFD ===
        // entry_count: 1
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2;

        // IFD entry: ImageWidth (tag=256, type=SHORT(3), count=1, value=64)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 256); pos += 2;  // tag: ImageWidth
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 3);   pos += 2;  // field_type: SHORT
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1);   pos += 4;  // count: 1
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 64);  pos += 4;  // value: 64

        // next_ifd_offset: 0 (no more IFDs)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0);

        return data;
    }

    /// <summary>
    /// RATIONAL型タグ付きTIFF(リトルエンディアン):
    /// header(8B) + IFD(2B + 12B entry + 4B next) + RATIONAL data(8B) = 34バイト
    /// XResolution(tag=282), field_type=5(RATIONAL), count=1, value_offset→外部RATIONAL(72/1)
    /// </summary>
    public static byte[] CreateTiffWithRationalTag()
    {
        var data = new byte[34];
        var span = data.AsSpan();
        var pos = 0;

        // === tiff header ===
        Encoding.ASCII.GetBytes("II").CopyTo(span[pos..]); pos += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 42); pos += 2;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 8); pos += 4; // ifd_offset

        // === IFD at offset 8 ===
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 1); pos += 2; // entry_count

        // IFD entry: XResolution (tag=282, type=RATIONAL(5), count=1, value_offset=26)
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 282); pos += 2; // tag
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 5); pos += 2;   // field_type: RATIONAL
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1); pos += 4;   // count
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 26); pos += 4;  // value_offset → offset 26

        // next_ifd_offset: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;

        // === RATIONAL data at offset 26 (8 bytes) ===
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 72); pos += 4;  // numerator
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1);             // denominator

        return data;
    }

    /// <summary>
    /// RATIONAL型タグ付きTIFF(ビッグエンディアン):
    /// header(8B) + IFD(2B + 12B entry + 4B next) + RATIONAL data(8B) = 34バイト
    /// </summary>
    public static byte[] CreateBigEndianTiffWithRational()
    {
        var data = new byte[34];
        var span = data.AsSpan();
        var pos = 0;

        // === tiff header ===
        Encoding.ASCII.GetBytes("MM").CopyTo(span[pos..]); pos += 2;
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 42); pos += 2;
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 8); pos += 4;

        // === IFD at offset 8 ===
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2;

        // IFD entry: XResolution (tag=282, type=RATIONAL(5), count=1, value_offset=26)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 282); pos += 2;
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 5); pos += 2;
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4;
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 26); pos += 4;

        // next_ifd_offset: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // === RATIONAL data at offset 26 (8 bytes, big-endian) ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 72); pos += 4;
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1);

        return data;
    }
}
