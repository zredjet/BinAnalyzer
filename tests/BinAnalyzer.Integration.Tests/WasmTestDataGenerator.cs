using System.Buffers.Binary;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class WasmTestDataGenerator
{
    /// <summary>
    /// 最小WASMバイナリ: magic(4B) + version(4B) + 1 section(section_id(1B) + section_size(1B LEB128) + section_data(4B)) = 14バイト
    /// Type section (id=1) を1つ含む
    /// </summary>
    public static byte[] CreateMinimalWasm()
    {
        var data = new byte[14];
        var span = data.AsSpan();
        var pos = 0;

        // magic: \0asm
        data[0] = 0x00;
        data[1] = 0x61; // 'a'
        data[2] = 0x73; // 's'
        data[3] = 0x6D; // 'm'
        pos = 4;

        // version: 1 (uint32 LE)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1); pos += 4;

        // section: type section (id=1)
        data[pos] = 1; pos += 1; // section_id = type

        // section_size: 4 bytes (LEB128, value 4 fits in 1 byte)
        data[pos] = 0x04; pos += 1;

        // section_data: 4 dummy bytes
        data[pos] = 0x01; pos += 1; // count = 1
        data[pos] = 0x60; pos += 1; // func type marker
        data[pos] = 0x00; pos += 1; // 0 params
        data[pos] = 0x00; pos += 1; // 0 results

        return data;
    }

    /// <summary>
    /// WASM with type section + export section:
    /// magic(4B) + version(4B) + type section(6B) + export section: id(1B) + size(1B) + data(8B) = 24バイト
    /// Export section exports "main" as function index 0
    /// </summary>
    public static byte[] CreateWasmWithExportSection()
    {
        var data = new byte[24];
        var span = data.AsSpan();
        var pos = 0;

        // magic
        data[0] = 0x00; data[1] = 0x61; data[2] = 0x73; data[3] = 0x6D;
        pos = 4;

        // version: 1
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1); pos += 4;

        // type section (id=1, size=4)
        data[pos] = 1; pos += 1;  // section_id
        data[pos] = 0x04; pos += 1; // section_size (LEB128)
        data[pos] = 0x01; pos += 1; // count=1
        data[pos] = 0x60; pos += 1; // func type marker
        data[pos] = 0x00; pos += 1; // 0 params
        data[pos] = 0x00; pos += 1; // 0 results

        // export section (id=7, size=8)
        data[pos] = 7; pos += 1;   // section_id
        data[pos] = 0x08; pos += 1; // section_size (LEB128, 8 bytes)
        // export_section_data: count=1
        data[pos] = 0x01; pos += 1;
        // export_entry: name_len=4, name="main", kind=0(function), index=0
        data[pos] = 0x04; pos += 1; // name_len
        data[pos] = (byte)'m'; pos += 1;
        data[pos] = (byte)'a'; pos += 1;
        data[pos] = (byte)'i'; pos += 1;
        data[pos] = (byte)'n'; pos += 1;
        data[pos] = 0x00; pos += 1; // kind: function
        data[pos] = 0x00; // index: 0

        return data;
    }

    /// <summary>
    /// WebAssembly 3.0 の機能を使うモジュール（REQ-188）。GC の型（再帰グループの struct・array）、関数・メモリ（最大値あり）・
    /// グローバル・タグのインポート、拡張定数式（i32.const 40 + 2）のグローバル、エクスポート、要素・データ（アクティブとパッシブ）、
    /// データの数、ローカル変数付きの関数の本体、name（モジュール名・関数名）と producers のカスタムセクションを持つ。
    /// </summary>
    public static byte[] CreateWasm3Module()
    {
        var ms = new MemoryStream();
        ms.Write([0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00]);
        void Section(byte id, byte[] content)
        {
            ms.WriteByte(id);
            WriteUleb(ms, (uint)content.Length);
            ms.Write(content);
        }
        byte[] Name(string v) { var b = Encoding.UTF8.GetBytes(v); var m = new MemoryStream(); WriteUleb(m, (uint)b.Length); m.Write(b); return m.ToArray(); }
        byte[] Cat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

        Section(1, Cat(
            [4],
            [0x60, 2, 0x7F, 0x7F, 1, 0x7F],                        // 0: (i32, i32) → i32
            [0x60, 0, 0],                                           // 1: () → ()
            [0x4E, 1, 0x50, 0, 0x5F, 2, 0x7F, 1, 0x63, 2, 0],       // 2: rec { sub struct { mut i32, (ref null 2) } }
            [0x5E, 0x78, 1]));                                      // 3: array (mut i8)
        Section(2, Cat(
            [4],
            Name("env"), Name("log"), [0, 1],                       // 関数（型 1）
            Name("env"), Name("mem"), [2, 1, 1, 2],                 // メモリ（最小 1、最大 2 ページ）
            Name("env"), Name("g"), [3, 0x7F, 0],                   // グローバル（i32、変更不可）
            Name("env"), Name("ex"), [4, 0, 1]));                   // タグ（型 1）
        Section(3, [1, 0]);                                         // 関数 1 個（型 0）
        Section(4, [1, 0x70, 0, 1]);                                // テーブル: funcref、最小 1
        Section(6, [1, 0x7F, 1, 0x41, 40, 0x41, 2, 0x6A, 0x0B]);   // グローバル: mut i32 = 40 + 2
        Section(7, Cat([2], Name("add"), [0, 1], Name("g2"), [3, 1]));
        Section(9, [1, 0, 0x41, 0, 0x0B, 1, 1]);                    // 要素: テーブル 0 の 0 番から関数 1
        Section(12, [2]);                                           // データの数
        Section(10, [1, 9, 1, 1, 0x7E, 0x20, 0, 0x20, 1, 0x6A, 0x0B]); // (local i64) local.get 0 local.get 1 i32.add
        Section(11, Cat([2, 0, 0x41, 16, 0x0B, 2], "hi"u8.ToArray(), [1, 3], "xyz"u8.ToArray()));
        var names = Cat(Name("name"), [0], Sized(Name("gen")), [1], Sized(Cat([1, 1], Name("add"))));
        Section(0, names);
        Section(0, Cat(Name("producers"), [1], Name("language"), [1], Name("C11"), Name("")));
        return ms.ToArray();

        static byte[] Sized(byte[] content)
        {
            var m = new MemoryStream();
            WriteUleb(m, (uint)content.Length);
            m.Write(content);
            return m.ToArray();
        }
    }

    private static void WriteUleb(Stream s, uint value)
    {
        do
        {
            var b = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0) b |= 0x80;
            s.WriteByte(b);
        } while (value != 0);
    }
}
