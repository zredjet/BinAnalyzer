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

    /// <summary>
    /// head + cmap + hhea テーブル付きOTF:
    /// offset_table(12B) + 3 table_records(48B) + head(54B) + cmap(12B) + hhea(36B) = 162バイト
    /// </summary>
    public static byte[] CreateOtfWithCmapAndHhea()
    {
        var data = new byte[162];
        var span = data.AsSpan();
        var pos = 0;

        // === offset_table (12 bytes) ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x4F54544F); pos += 4; // OTTO
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 3); pos += 2; // numTables
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 32); pos += 2; // searchRange
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2; // entrySelector
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 16); pos += 2; // rangeShift

        // === table_record[0]: head (16 bytes) ===
        // Tables start at offset: 12 + 3*16 = 60
        Encoding.ASCII.GetBytes("head").CopyTo(span[pos..]); pos += 4;
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // checksum
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 60); pos += 4; // offset
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 54); pos += 4; // length

        // === table_record[1]: cmap (16 bytes) ===
        Encoding.ASCII.GetBytes("cmap").CopyTo(span[pos..]); pos += 4;
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // checksum
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 114); pos += 4; // offset (60+54)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 12); pos += 4; // length

        // === table_record[2]: hhea (16 bytes) ===
        Encoding.ASCII.GetBytes("hhea").CopyTo(span[pos..]); pos += 4;
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // checksum
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 126); pos += 4; // offset (114+12)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 36); pos += 4; // length

        // === head_table (at offset 60, 54 bytes) ===
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2; // majorVersion
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2; // minorVersion
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x00010000); pos += 4; // fontRevision
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // checksumAdjustment
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x5F0F3CF5); pos += 4; // magicNumber
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x000B); pos += 2; // flags
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1000); pos += 2; // unitsPerEm
        BinaryPrimitives.WriteInt64BigEndian(span[pos..], 0); pos += 8; // created
        BinaryPrimitives.WriteInt64BigEndian(span[pos..], 0); pos += 8; // modified
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 0); pos += 2; // xMin
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 0); pos += 2; // yMin
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 100); pos += 2; // xMax
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 100); pos += 2; // yMax
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2; // macStyle
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 8); pos += 2; // lowestRecPPEM
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 2); pos += 2; // fontDirectionHint
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 0); pos += 2; // indexToLocFormat
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 0); pos += 2; // glyphDataFormat

        // === cmap_table (at offset 114, 12 bytes) ===
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2; // version
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2; // numTables
        // encoding_record: platformID=3 (Windows), encodingID=1, offset=12
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 3); pos += 2; // platformID
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2; // encodingID
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 12); pos += 4; // offset

        // === hhea_table (at offset 126, 36 bytes) ===
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2; // majorVersion
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2; // minorVersion
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 800); pos += 2; // ascender
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], -200); pos += 2; // descender
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 0); pos += 2; // lineGap
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1000); pos += 2; // advanceWidthMax
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], -100); pos += 2; // minLeftSideBearing
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], -100); pos += 2; // minRightSideBearing
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 900); pos += 2; // xMaxExtent
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 1); pos += 2; // caretSlopeRise
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 0); pos += 2; // caretSlopeRun
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 0); pos += 2; // caretOffset
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 0); pos += 2; // reserved1
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 0); pos += 2; // reserved2
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 0); pos += 2; // reserved3
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 0); pos += 2; // reserved4
        BinaryPrimitives.WriteInt16BigEndian(span[pos..], 0); pos += 2; // metricDataFormat
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 256); // numberOfHMetrics

        return data;
    }
}
