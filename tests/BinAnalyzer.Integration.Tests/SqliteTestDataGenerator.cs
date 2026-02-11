using System.Buffers.Binary;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class SqliteTestDataGenerator
{
    /// <summary>
    /// 最小SQLiteデータベース: database_header(100B) + btree_page_header(8B) = 108バイト
    /// ページサイズ4096, UTF-8, レガシーフォーマット, リーフテーブルB-Treeページ
    /// </summary>
    public static byte[] CreateMinimalSqlite()
    {
        var data = new byte[108];
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

        // cell_content_offset: 0 (means 65536)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // fragmented_free_bytes: 0
        data[pos] = 0;

        return data;
    }

    /// <summary>
    /// セル付きSQLiteデータベース:
    /// database_header(100B) + btree_page_header(8B, number_of_cells=1)
    /// + cell_pointer_array(2B) + table_leaf_cell(1+1+2=4B) = 114バイト
    /// </summary>
    public static byte[] CreateSqliteWithCell()
    {
        var data = new byte[114];
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

        // === table_leaf_cell at offset 110 (4 bytes) ===
        // payload_size: 2 (uleb128: single byte 0x02)
        data[pos] = 0x02; pos += 1;
        // rowid: 1 (uleb128: single byte 0x01)
        data[pos] = 0x01; pos += 1;
        // payload: 2 bytes
        data[pos] = 0xAB;
        data[pos + 1] = 0xCD;

        return data;
    }
}
