using System.Buffers.Binary;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class SqliteTestDataGenerator
{
    /// <summary>
    /// 最小SQLiteデータベース: database_header(100B) + btree_page_header(8B) を先頭に持つ 1 ページ（4096 バイト）
    /// ページサイズ4096, UTF-8, レガシーフォーマット, リーフテーブルB-Treeページ
    /// </summary>
    public static byte[] CreateMinimalSqlite()
    {
        var data = new byte[4096]; // 1 ページ分（REQ-188 で 108 バイトから 1 ページに伸ばした。ページの途中で切れたファイルにしない）
        var span = data.AsSpan();
        var pos = 0;

        // === database_header (100 bytes) ===
        // magic: "SQLite format 3\000" (16 bytes)
        Encoding.ASCII.GetBytes("SQLite format 3").CopyTo(span[pos..]);
        data[pos + 15] = 0x00;
        pos = 16;

        // page_size: 4096
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 4096); pos += 2;

        // write_version: 1 (legacy)
        data[pos] = 1; pos += 1;

        // read_version: 1 (legacy)
        data[pos] = 1; pos += 1;

        // reserved_space: 0
        data[pos] = 0; pos += 1;

        // max_embedded_payload_fraction: 64
        data[pos] = 64; pos += 1;

        // min_embedded_payload_fraction: 32
        data[pos] = 32; pos += 1;

        // leaf_payload_fraction: 32
        data[pos] = 32; pos += 1;

        // file_change_counter: 1
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4;

        // database_size_pages: 1
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4;

        // first_freelist_trunk_page: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // total_freelist_pages: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // schema_cookie: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // schema_format_number: 4
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 4); pos += 4;

        // default_page_cache_size: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // largest_root_btree_page: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // text_encoding: 1 (UTF-8)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4;

        // user_version: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // incremental_vacuum_mode: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // application_id: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // reserved_for_expansion: 20 bytes of zeros
        pos += 20;

        // version_valid_for: 1
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4;

        // sqlite_version_number: 3039004
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 3039004); pos += 4;

        // pos should now be 100

        // === btree_page_header (8 bytes for leaf page) ===
        // page_type: 13 (TABLE_LEAF)
        data[pos] = 13; pos += 1;

        // first_freeblock: 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // number_of_cells: 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // cell_content_offset: 4096（セルの無いページはページの末尾。0 は 65536 の意味なので 4096 のページでは不正）
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 4096); pos += 2;

        // fragmented_free_bytes: 0
        data[pos] = 0;

        return data;
    }

    /// <summary>
    /// セル付きSQLiteデータベース:
    /// database_header(100B) + btree_page_header(8B, number_of_cells=1)
    /// + cell_pointer_array(2B) + table_leaf_cell(1+1+3=5B) を先頭に持つ 1 ページ（4096 バイト）
    /// </summary>
    public static byte[] CreateSqliteWithCell()
    {
        var data = new byte[4096]; // 1 ページ分（REQ-188 で 114 バイトから 1 ページに伸ばした）
        var span = data.AsSpan();
        var pos = 0;

        // === database_header (100 bytes) — same as minimal ===
        Encoding.ASCII.GetBytes("SQLite format 3").CopyTo(span[pos..]);
        data[pos + 15] = 0x00;
        pos = 16;
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 4096); pos += 2; // page_size
        data[pos] = 1; pos += 1; // write_version
        data[pos] = 1; pos += 1; // read_version
        data[pos] = 0; pos += 1; // reserved_space
        data[pos] = 64; pos += 1; // max_embedded
        data[pos] = 32; pos += 1; // min_embedded
        data[pos] = 32; pos += 1; // leaf_payload
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4; // file_change_counter
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4; // database_size_pages
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // first_freelist
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // total_freelist
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // schema_cookie
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 4); pos += 4; // schema_format_number
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // default_page_cache_size
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // largest_root_btree_page
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4; // text_encoding (UTF-8)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // user_version
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // incremental_vacuum
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // application_id
        pos += 20; // reserved_for_expansion
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4; // version_valid_for
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 3039004); pos += 4; // sqlite_version_number
        // pos = 100

        // === btree_page_header (8 bytes for leaf page) ===
        data[pos] = 13; pos += 1; // page_type: TABLE_LEAF
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2; // first_freeblock
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2; // number_of_cells=1
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 110); pos += 2; // cell_content_offset
        data[pos] = 0; pos += 1; // fragmented_free_bytes
        // pos = 108

        // === cell_pointer_array (2 bytes for 1 cell) ===
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 110); pos += 2; // cell pointer to offset 110
        // pos = 110

        // === table_leaf_cell at offset 110 (5 bytes) ===
        // payload_size: 3（可変長整数 1 バイト）
        data[pos] = 0x03; pos += 1;
        // rowid: 1
        data[pos] = 0x01; pos += 1;
        // payload: 記録（ヘッダ長 2、シリアル型 1 = 8 ビットの整数、値 42）。REQ-188 で記録として正しい形に直した
        data[pos] = 0x02;
        data[pos + 1] = 0x01;
        data[pos + 2] = 0x2A;

        return data;
    }

    // ===== REQ-188: 記録・シリアル型・あふれページ =====
    private const int SmallPage = 512;

    /// <summary>SQLite の可変長整数（ビッグエンディアンの 7 ビットずつ。9 バイト目は使わない範囲）。</summary>
    private static byte[] SqliteVarint(long value)
    {
        var bytes = new List<byte> { (byte)(value & 0x7F) };
        value >>= 7;
        while (value > 0) { bytes.Insert(0, (byte)((value & 0x7F) | 0x80)); value >>= 7; }
        return bytes.ToArray();
    }

    /// <summary>記録（ヘッダ = ヘッダ長 + シリアル型の並び、続けて値）。値は (シリアル型, バイト列) の組。</summary>
    private static byte[] Record(params (long SerialType, byte[] Value)[] columns)
    {
        var types = columns.SelectMany(c => SqliteVarint(c.SerialType)).ToArray();
        var headerSize = types.Length + 1;
        if (SqliteVarint(headerSize).Length > 1) headerSize++;
        return SqliteVarint(headerSize).Concat(types).Concat(columns.SelectMany(c => c.Value)).ToArray();
    }

    private static (long, byte[]) Text(string s) { var b = Encoding.UTF8.GetBytes(s); return (13 + 2L * b.Length, b); }
    private static (long, byte[]) Blob(byte[] b) => (12 + 2L * b.Length, b);
    private static (long, byte[]) Int8(sbyte v) => (1, [(byte)v]);
    private static (long, byte[]) Int24(int v) => (3, [(byte)(v >> 16), (byte)(v >> 8), (byte)v]);
    private static (long, byte[]) Int48(long v) => (5, [(byte)(v >> 40), (byte)(v >> 32), (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v]);
    private static (long, byte[]) Real(double v) { var b = BitConverter.GetBytes(v); Array.Reverse(b); return (7, b); }
    private static (long, byte[]) Null() => (0, []);
    private static (long, byte[]) Zero() => (8, []);
    private static (long, byte[]) One() => (9, []);

    /// <summary>表の葉のページ（ページ内の位置 headerOffset から B 木のヘッダ）。セルは後ろから詰める。</summary>
    private static void WriteTableLeaf(byte[] page, int headerOffset, IReadOnlyList<byte[]> cells)
    {
        var contentStart = SmallPage;
        var pointers = new List<int>();
        foreach (var cell in cells)
        {
            contentStart -= cell.Length;
            cell.CopyTo(page, contentStart);
            pointers.Add(contentStart);
        }
        page[headerOffset] = 13;
        BinaryPrimitives.WriteUInt16BigEndian(page.AsSpan(headerOffset + 3), (ushort)cells.Count);
        BinaryPrimitives.WriteUInt16BigEndian(page.AsSpan(headerOffset + 5), (ushort)contentStart);
        for (var i = 0; i < pointers.Count; i++)
            BinaryPrimitives.WriteUInt16BigEndian(page.AsSpan(headerOffset + 8 + i * 2), (ushort)pointers[i]);
    }

    private static byte[] LeafCell(long rowid, byte[] record, int localSize = -1, uint overflowPage = 0)
    {
        var local = localSize < 0 ? record : record[..localSize];
        var cell = SqliteVarint(record.Length).Concat(SqliteVarint(rowid)).Concat(local);
        if (localSize >= 0) cell = cell.Concat(new[] { (byte)(overflowPage >> 24), (byte)(overflowPage >> 16), (byte)(overflowPage >> 8), (byte)overflowPage });
        return cell.ToArray();
    }

    /// <summary>
    /// 記録を持つ 3 ページのデータベース（ページサイズ 512、REQ-188）。1 ページ目はスキーマ（CREATE TABLE t(a, b, c, d, e)）、
    /// 2 ページ目は表 t の葉で、行 1 = (NULL, -1, 70000, 'abc', 1.5)、行 2 = (0, 1, 2^40, x'00FF', 'こんにちは')、
    /// 行 3 = 608 文字の文字列 1 列（ペイロード 611 バイトのうち 103 バイトがページ内、残りが 3 ページ目のあふれページ）。
    /// sqlite3 の PRAGMA integrity_check が ok になり、SELECT で同じ値が返ることを確かめた。
    /// </summary>
    public static byte[] CreateSqliteWithRecords()
    {
        var db = new byte[SmallPage * 3];
        "SQLite format 3\0"u8.CopyTo(db);
        BinaryPrimitives.WriteUInt16BigEndian(db.AsSpan(16), SmallPage);
        db[18] = 1; db[19] = 1; db[20] = 0; db[21] = 64; db[22] = 32; db[23] = 32;
        BinaryPrimitives.WriteUInt32BigEndian(db.AsSpan(24), 1);      // file_change_counter
        BinaryPrimitives.WriteUInt32BigEndian(db.AsSpan(28), 3);      // database_size_pages
        BinaryPrimitives.WriteUInt32BigEndian(db.AsSpan(40), 1);      // schema_cookie
        BinaryPrimitives.WriteUInt32BigEndian(db.AsSpan(44), 4);      // schema_format_number
        BinaryPrimitives.WriteUInt32BigEndian(db.AsSpan(56), 1);      // text_encoding = UTF-8
        BinaryPrimitives.WriteUInt32BigEndian(db.AsSpan(92), 1);      // version_valid_for
        BinaryPrimitives.WriteUInt32BigEndian(db.AsSpan(96), 3050000);

        var page1 = new byte[SmallPage];
        var schema = Record(Text("table"), Text("t"), Text("t"), Int8(2), Text("CREATE TABLE t(a, b, c, d, e)"));
        WriteTableLeaf(page1, 100, [LeafCell(1, schema)]);
        page1[..SmallPage].AsSpan(100).CopyTo(db.AsSpan(100));

        var row1 = Record(Null(), Int8(-1), Int24(70000), Text("abc"), Real(1.5));
        var row2 = Record(Zero(), One(), Int48(1L << 40), Blob([0x00, 0xFF]), Text("こんにちは"));
        var row3 = Record(Text(new string('x', 608)));
        // U = 512: X = U - 35 = 477、M = ((U - 12) × 32 / 255) - 23 = 39、K = M + (P - M) % (U - 4) = 39 + 572 % 508 = 103
        var page2 = new byte[SmallPage];
        WriteTableLeaf(page2, 0, [LeafCell(1, row1), LeafCell(2, row2), LeafCell(3, row3, localSize: 103, overflowPage: 3)]);
        page2.CopyTo(db, SmallPage);

        var page3 = db.AsSpan(SmallPage * 2);
        BinaryPrimitives.WriteUInt32BigEndian(page3, 0);               // 次のあふれページ無し
        row3.AsSpan(103).CopyTo(page3[4..]);
        return db;
    }
}
