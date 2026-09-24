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
        // 4 バイトの値欄に収まる SHORT は先頭に詰める（TIFF 6.0: left-justified）。BE では 00 40 00 00
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 64);  pos += 4;  // value: 64

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

    /// <summary>
    /// 2 ページの TIFF（LE）。IFD0: ImageWidth=3、Compression=5（LZW）→ IFD1: ImageWidth=5（REQ-188: next_ifd の連なり）。
    /// </summary>
    public static byte[] CreateTwoPageTiff()
    {
        var data = new byte[8 + (2 + 2 * 12 + 4) + (2 + 12 + 4)];
        var s = data.AsSpan();
        "II"u8.CopyTo(s);
        BinaryPrimitives.WriteUInt16LittleEndian(s[2..], 42);
        BinaryPrimitives.WriteUInt32LittleEndian(s[4..], 8);
        var ifd1 = 8 + 2 + 2 * 12 + 4;
        WriteClassicIfd(s[8..], [(256, 3, 3), (259, 3, 5)], (uint)ifd1);
        WriteClassicIfd(s[ifd1..], [(256, 3, 5)], 0);
        return data;
    }

    private static void WriteClassicIfd(Span<byte> s, (ushort Tag, ushort Type, ushort Value)[] entries, uint next)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(s, (ushort)entries.Length);
        for (var i = 0; i < entries.Length; i++)
        {
            var e = s[(2 + i * 12)..];
            BinaryPrimitives.WriteUInt16LittleEndian(e, entries[i].Tag);
            BinaryPrimitives.WriteUInt16LittleEndian(e[2..], entries[i].Type);
            BinaryPrimitives.WriteUInt32LittleEndian(e[4..], 1);
            BinaryPrimitives.WriteUInt16LittleEndian(e[8..], entries[i].Value);   // SHORT は値欄の先頭 2 バイト
        }
        BinaryPrimitives.WriteUInt32LittleEndian(s[(2 + entries.Length * 12)..], next);
    }

    /// <summary>
    /// BigTIFF（BE、2 ページ）。IFD0: ImageWidth=LONG 640、Compression=SHORT 8（Deflate）、StripOffsets=LONG8 → IFD1: ImageWidth=SHORT 7（REQ-188）。
    /// </summary>
    public static byte[] CreateBigTiff()
    {
        const int ifd0 = 16, entrySize = 20;
        var ifd1 = ifd0 + 8 + 3 * entrySize + 8;
        var data = new byte[ifd1 + 8 + entrySize + 8];
        var s = data.AsSpan();
        "MM"u8.CopyTo(s);
        BinaryPrimitives.WriteUInt16BigEndian(s[2..], 43);
        BinaryPrimitives.WriteUInt16BigEndian(s[4..], 8);
        BinaryPrimitives.WriteUInt16BigEndian(s[6..], 0);
        BinaryPrimitives.WriteUInt64BigEndian(s[8..], ifd0);

        void Entry(int at, ushort tag, ushort type, ulong valueLeftJustified)
        {
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(at), tag);
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(at + 2), type);
            BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(at + 4), 1);
            BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(at + 12), valueLeftJustified);
        }
        BinaryPrimitives.WriteUInt64BigEndian(s[ifd0..], 3);
        Entry(ifd0 + 8, 256, 4, 640UL << 32);                     // LONG は値欄の先頭 4 バイト
        Entry(ifd0 + 8 + entrySize, 259, 3, 8UL << 48);          // SHORT は値欄の先頭 2 バイト
        Entry(ifd0 + 8 + 2 * entrySize, 273, 16, 0x1_0000_0000);  // LONG8（4 GiB を超えるオフセット）
        BinaryPrimitives.WriteUInt64BigEndian(s[(ifd0 + 8 + 3 * entrySize)..], (ulong)ifd1);
        BinaryPrimitives.WriteUInt64BigEndian(s[ifd1..], 1);
        Entry(ifd1 + 8, 256, 3, 7UL << 48);
        BinaryPrimitives.WriteUInt64BigEndian(s[(ifd1 + 8 + entrySize)..], 0);
        return data;
    }
}
