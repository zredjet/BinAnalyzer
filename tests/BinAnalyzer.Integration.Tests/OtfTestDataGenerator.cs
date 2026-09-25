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
    /// 最小TTFファイル: CreateMinimalOtf() と同一構造、sfVersion のみ TrueType (0x00010000) に変更
    /// </summary>
    public static byte[] CreateMinimalTtf()
    {
        var data = CreateMinimalOtf();
        var span = data.AsSpan();
        // sfVersion: 0x00010000 (TrueType) — 先頭4バイトを上書き
        BinaryPrimitives.WriteUInt32BigEndian(span, 0x00010000);
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

    // ===== REQ-188: 主な表と TTC =====
    private static byte[] U16(params int[] v) => v.SelectMany(x => new[] { (byte)(x >> 8), (byte)x }).ToArray();
    private static byte[] U32(params long[] v) => v.SelectMany(x => new[] { (byte)(x >> 24), (byte)(x >> 16), (byte)(x >> 8), (byte)x }).ToArray();
    private static byte[] Cat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

    private static byte[] HeadTable() => Cat(U16(1, 0), U32(0x00010000, 0, 0x5F0F3CF5), U16(0x000B, 1000),
        U32(0, 3786825600L, 0, 3786825600L), U16(0, 0xFFE0 - 0x10000 + 0x10000, 500, 800, 0x0001, 8, 2, 0, 0));

    private static byte[] NameTable()
    {
        (int Platform, int Encoding, int Language, int NameId, byte[] Text)[] records =
        [
            (1, 0, 0, 1, System.Text.Encoding.Latin1.GetBytes("Test Sans")),
            (3, 1, 0x0409, 1, System.Text.Encoding.BigEndianUnicode.GetBytes("Test Sans")),
            (3, 1, 0x0409, 2, System.Text.Encoding.BigEndianUnicode.GetBytes("Regular")),
            (3, 1, 0x0411, 4, System.Text.Encoding.BigEndianUnicode.GetBytes("テスト サンス")),
            (3, 1, 0x0409, 256, System.Text.Encoding.BigEndianUnicode.GetBytes("Weight")),
        ];
        var strings = new MemoryStream();
        var recordBytes = new MemoryStream();
        foreach (var r in records)
        {
            recordBytes.Write(U16(r.Platform, r.Encoding, r.Language, r.NameId, r.Text.Length, (int)strings.Position));
            strings.Write(r.Text);
        }
        return Cat(U16(0, records.Length, 6 + records.Length * 12), recordBytes.ToArray(), strings.ToArray());
    }

    private static byte[] CmapTable()
    {
        // 形式 4: 'A'〜'C'（0x41〜0x43）→ グリフ 1〜3、最後の区間 0xFFFF
        var format4 = Cat(U16(4, 32, 0, 4, 4, 1, 0), U16(0x0043, 0xFFFF), U16(0), U16(0x0041, 0xFFFF), U16(0x10000 - 0x40, 1), U16(0, 0));
        // 形式 12: U+1F600 → グリフ 4
        var format12 = Cat(U16(12, 0), U32(28, 0, 1), U32(0x1F600, 0x1F600, 4));
        var header = Cat(U16(0, 2), U16(3, 1), U32(20), U16(3, 10), U32(20 + format4.Length));
        return Cat(header, format4, format12);
    }

    private static byte[] Os2Table() => Cat(U16(4, 500, 400, 5, 0x0008), U16(650, 700, 0, 140, 650, 700, 0, 480, 50, 250, 0),
        new byte[] { 2, 11, 6, 3, 3, 5, 4, 2, 2, 4 }, U32(1, 0, 0, 0), "TEST"u8.ToArray(), U16(0x0040, 0x0041, 0xFFFF),
        U16(800, 0x10000 - 200, 90, 1000, 200), U32(1, 0), U16(500, 700, 0, 0x20, 2));

    private static byte[] PostTable() => Cat(U32(0x00020000, 0), U16(0x10000 - 100, 50), U32(0, 0, 0, 0, 0),
        U16(5), U16(0, 36, 37, 38, 258), new byte[] { 8 }, "smileyfc"u8.ToArray());

    private static byte[] FvarTable() => Cat(U16(1, 0, 16, 2, 1, 20, 2, 10),
        "wght"u8.ToArray(), U32(100L << 16, 400L << 16, 900L << 16), U16(0, 256),
        U16(2, 0), U32(400L << 16), U16(0xFFFF),
        U16(2, 0), U32(700L << 16), U16(0xFFFF));

    // GSUB: ScriptList（10: latn → 既定の LangSys が機能 0）、FeatureList（30: liga、ルックアップ無し）、LookupList（42: 0 個）
    private static byte[] GsubTable() => Cat(U16(1, 0, 10, 30, 42),
        U16(1), "latn"u8.ToArray(), U16(8), U16(4, 0), U16(0, 0xFFFF, 1, 0),
        U16(1), "liga"u8.ToArray(), U16(8), U16(0, 0),
        U16(0));

    private static byte[] Sfnt(uint sfVersion, (string Tag, byte[] Data)[] tables, int baseOffset = 0, IReadOnlyList<int>? sharedOffsets = null)
    {
        var directory = new MemoryStream();
        var body = new MemoryStream();
        var headerSize = 12 + tables.Length * 16;
        directory.Write(U32(sfVersion));
        var power = 1; var log = 0;
        while (power * 2 <= tables.Length) { power *= 2; log++; }
        directory.Write(U16(tables.Length, power * 16, log, tables.Length * 16 - power * 16));
        for (var i = 0; i < tables.Length; i++)
        {
            var offset = sharedOffsets?[i] ?? baseOffset + headerSize + (int)body.Position;
            directory.Write(System.Text.Encoding.ASCII.GetBytes(tables[i].Tag));
            directory.Write(U32(0, offset, tables[i].Data.Length));
            if (sharedOffsets is null)
            {
                body.Write(tables[i].Data);
                while (body.Position % 4 != 0) body.WriteByte(0);
            }
        }
        return Cat(directory.ToArray(), body.ToArray());
    }

    private static (string, byte[])[] SampleTables() =>
    [
        ("GSUB", GsubTable()),
        ("OS/2", Os2Table()),
        ("cmap", CmapTable()),
        ("fvar", FvarTable()),
        ("head", HeadTable()),
        ("hhea", Cat(U16(1, 0, 800, 0x10000 - 200, 90, 1000, 0, 0, 900, 1, 0, 0, 0, 0, 0, 0, 0, 5))),
        ("maxp", Cat(U32(0x00005000), U16(5))),
        ("name", NameTable()),
        ("post", PostTable()),
    ];

    /// <summary>
    /// 主な表を持つ TrueType のフォント（REQ-188）: GSUB（latn・liga）・OS/2（版 4）・cmap（形式 4 = 'A'〜'C'、形式 12 = U+1F600）・fvar（wght 100〜900、インスタンス 2）・
    /// head・hhea・maxp（版 0.5、グリフ 5）・name（Mac の Latin-1、Windows の英語と日本語の UTF-16BE）・post（版 2.0、独自のグリフ名 1 つ）。
    /// fontTools 4.63 で各表を読んで値が一致することを確かめた。
    /// </summary>
    public static byte[] CreateTtfWithTables() => Sfnt(0x00010000, SampleTables());

    /// <summary>
    /// 2 つのフォントの TTC（版 2.0、REQ-188）。2 つ目のフォントは 1 つ目の表を共有する（表のレコードが同じ位置を指す）。DSIG は無し。
    /// fontTools の TTCollection で 2 つのフォントとして読めることを確かめた。
    /// </summary>
    public static byte[] CreateTtc()
    {
        var tables = SampleTables();
        const int ttcHeader = 12 + 4 * 2 + 12;
        var first = Sfnt(0x00010000, tables, baseOffset: ttcHeader);
        var directorySize = 12 + tables.Length * 16;
        var offsets = new List<int>();
        var position = ttcHeader + directorySize;
        foreach (var (_, data) in tables) { offsets.Add(position); position += (data.Length + 3) / 4 * 4; }
        var second = Sfnt(0x00010000, tables, sharedOffsets: offsets);
        var header = Cat("ttcf"u8.ToArray(), U16(2, 0), U32(2, ttcHeader, ttcHeader + first.Length), U32(0, 0, 0));
        return Cat(header, first, second);
    }
}
