using System.Buffers.Binary;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class OtfTestDataGenerator
{
    /// <summary>
    /// 最小OTFファイル: offset_table(12B) + 1 table_record(16B + head_table(54B)) = 82バイト
    /// CFF(OTTO) フォント、headテーブル1つのみ
    /// </summary>
    public static byte[] CreateMinimalOtf()
    {
        var data = new byte[82];
        var span = data.AsSpan();
        var pos = 0;

        // === offset_table ===
        // sfVersion: 0x4F54544F = "OTTO" (CFF)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x4F54544F); pos += 4;

        // numTables: 1
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2;

        // searchRange: 16
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 16); pos += 2;

        // entrySelector: 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // rangeShift: 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // === table_record (16 bytes) ===
        // tag: "head"
        Encoding.ASCII.GetBytes("head").CopyTo(span[pos..]); pos += 4;

        // checksum: 0 (dummy)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // offset: 28 (12 offset_table + 16 table_record)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 28); pos += 4;

        // length: 54
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 54); pos += 4;

        // === head_table (at offset 28, 54 bytes) ===
        // majorVersion: 1
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2;

        // minorVersion: 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // fontRevision: 0x00010000 (1.0)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x00010000); pos += 4;

        // checksumAdjustment: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // magicNumber: 0x5F0F3CF5
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x5F0F3CF5); pos += 4;

        // flags: 0x000B
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x000B); pos += 2;

        // unitsPerEm: 1000
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1000); pos += 2;

        // created: 0 (int64)
        BinaryPrimitives.WriteInt64BigEndian(span[pos..], 0); pos += 8;

        // modified: 0 (int64)
        BinaryPrimitives.WriteInt64BigEndian(span[pos..], 0); pos += 8;

        // xMin: 0
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 0); pos += 2;
        // yMin: 0
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 0); pos += 2;
        // xMax: 100
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 100); pos += 2;
        // yMax: 100
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 100); pos += 2;

        // macStyle: 2 bytes bitfield (0 = no bold/italic)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // lowestRecPPEM: 8
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 8); pos += 2;

        // fontDirectionHint: 2
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 2); pos += 2;

        // indexToLocFormat: 0
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 0); pos += 2;

        // glyphDataFormat: 0
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 0);

        return data;
    }
}
