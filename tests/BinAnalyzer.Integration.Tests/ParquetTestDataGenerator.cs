using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class ParquetTestDataGenerator
{
    /// <summary>
    /// 最小の Parquet（REQ-188 で有効なファイルに変更）: CreateTwoColumnParquet から name の列を除いた 1 列 3 行。
    /// 以前は 'PAR1' + 長さ 0 + 'PAR1' の 12 バイトで、フッタの無い形だった
    /// </summary>
    public static byte[] CreateMinimalParquet() => Build(withNameColumn: false);

    /// <summary>Thrift Compact Protocol の書き出し（テストデータ用の最小限）。</summary>
    private sealed class ThriftWriter
    {
        private readonly MemoryStream _ms = new();
        private readonly Stack<int> _lastIds = new();
        private int _lastId;

        public void BeginStruct() { _lastIds.Push(_lastId); _lastId = 0; }
        public void EndStruct() { _ms.WriteByte(0); _lastId = _lastIds.Pop(); }
        public void Field(int id, int type)
        {
            var delta = id - _lastId;
            if (delta is > 0 and <= 15) _ms.WriteByte((byte)((delta << 4) | type));
            else { _ms.WriteByte((byte)type); Varint(ZigZag(id)); }
            _lastId = id;
        }
        public void Varint(ulong v) { while (v >= 0x80) { _ms.WriteByte((byte)(v | 0x80)); v >>= 7; } _ms.WriteByte((byte)v); }
        public static ulong ZigZag(long v) => (ulong)((v << 1) ^ (v >> 63));
        public void I32(int id, int v) { Field(id, 5); Varint(ZigZag(v)); }
        public void I64(int id, long v) { Field(id, 6); Varint(ZigZag(v)); }
        public void Binary(int id, string s) { Field(id, 8); RawBinary(s); }
        public void RawBinary(string s) { var b = Encoding.UTF8.GetBytes(s); Varint((ulong)b.Length); _ms.Write(b); }
        public void ListHeader(int id, int count, int elementType) { Field(id, 9); _ms.WriteByte((byte)((count << 4) | elementType)); }
        public void StructField(int id) { Field(id, 12); BeginStruct(); }
        public byte[] ToArray() => _ms.ToArray();
    }

    private static byte[] PageHeader(int valueBytes, int count)
    {
        var w = new ThriftWriter();
        w.BeginStruct();
        w.I32(1, 0);             // type = DATA_PAGE
        w.I32(2, valueBytes);    // uncompressed_page_size
        w.I32(3, valueBytes);    // compressed_page_size
        w.StructField(5);        // data_page_header
        w.I32(1, count);         // num_values
        w.I32(2, 0);             // encoding = PLAIN
        w.I32(3, 3);             // definition_level_encoding = RLE
        w.I32(4, 3);             // repetition_level_encoding = RLE
        w.EndStruct();
        w.EndStruct();
        return w.ToArray();
    }

    /// <summary>
    /// 2 列 3 行の Parquet（REQ-188）: 列 id（INT32 REQUIRED、1・2・3）と name（BYTE_ARRAY / UTF8 REQUIRED、"a"・"bb"・"ccc"）。
    /// 各列は圧縮なしの PLAIN のデータページ 1 つ。フッタは FileMetaData（版 1、スキーマ 3 要素、行グループ 1、キーと値 1、created_by）。
    /// pyarrow 22 で読めて値が一致することを確かめた。
    /// </summary>
    public static byte[] CreateTwoColumnParquet() => Build(withNameColumn: true);

    private static byte[] Build(bool withNameColumn)
    {
        var ms = new MemoryStream();
        ms.Write("PAR1"u8);
        var idValues = new byte[] { 1, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0 };
        var nameValues = new byte[] { 1, 0, 0, 0, (byte)'a', 2, 0, 0, 0, (byte)'b', (byte)'b', 3, 0, 0, 0, (byte)'c', (byte)'c', (byte)'c' };
        var idOffset = ms.Position;
        var idHeader = PageHeader(idValues.Length, 3);
        ms.Write(idHeader); ms.Write(idValues);
        var nameOffset = ms.Position;
        var nameHeader = PageHeader(nameValues.Length, 3);
        if (withNameColumn) { ms.Write(nameHeader); ms.Write(nameValues); }

        var w = new ThriftWriter();
        w.BeginStruct();
        w.I32(1, 1);                                     // version
        w.ListHeader(2, withNameColumn ? 3 : 2, 12);     // schema
        w.BeginStruct(); w.Binary(4, "schema"); w.I32(5, withNameColumn ? 2 : 1); w.EndStruct();
        w.BeginStruct(); w.I32(1, 1); w.I32(3, 0); w.Binary(4, "id"); w.EndStruct();
        if (withNameColumn) { w.BeginStruct(); w.I32(1, 6); w.I32(3, 0); w.Binary(4, "name"); w.I32(6, 0); w.EndStruct(); }
        w.I64(3, 3);                                     // num_rows
        w.ListHeader(4, 1, 12);                          // row_groups
        w.BeginStruct();
        w.ListHeader(1, withNameColumn ? 2 : 1, 12);     // columns
        void Column(string path, int type, long offset, int size)
        {
            w.BeginStruct();
            w.I64(2, offset);                            // file_offset
            w.StructField(3);                            // meta_data
            w.I32(1, type);
            w.ListHeader(2, 2, 5); w.Varint(ThriftWriter.ZigZag(0)); w.Varint(ThriftWriter.ZigZag(3)); // encodings = [PLAIN, RLE]
            w.ListHeader(3, 1, 8); w.RawBinary(path);    // path_in_schema
            w.I32(4, 0);                                 // codec = UNCOMPRESSED
            w.I64(5, 3);                                 // num_values
            w.I64(6, size);                              // total_uncompressed_size
            w.I64(7, size);                              // total_compressed_size
            w.I64(9, offset);                            // data_page_offset
            w.EndStruct();
            w.EndStruct();
        }
        Column("id", 1, idOffset, idHeader.Length + idValues.Length);
        if (withNameColumn) Column("name", 6, nameOffset, nameHeader.Length + nameValues.Length);
        w.I64(2, idHeader.Length + idValues.Length + (withNameColumn ? nameHeader.Length + nameValues.Length : 0)); // total_byte_size
        w.I64(3, 3);                                     // num_rows
        w.EndStruct();
        w.ListHeader(5, 1, 12);                          // key_value_metadata
        w.BeginStruct(); w.Binary(1, "origin"); w.Binary(2, "binanalyzer"); w.EndStruct();
        w.Binary(6, "binanalyzer-test");                 // created_by
        w.EndStruct();
        var footer = w.ToArray();
        ms.Write(footer);
        ms.Write(BitConverter.GetBytes(footer.Length));
        ms.Write("PAR1"u8);
        return ms.ToArray();
    }
}
