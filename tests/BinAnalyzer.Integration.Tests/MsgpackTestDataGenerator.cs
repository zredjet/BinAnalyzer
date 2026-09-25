using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class MsgpackTestDataGenerator
{
    /// <summary>
    /// 最小MessagePackファイル (6 bytes):
    /// fixmap(1 entry) + fixstr("key") + positive fixint(42)
    /// 81 a3 6b 65 79 2a
    /// </summary>
    public static byte[] CreateMinimalMsgpack()
    {
        return new byte[]
        {
            0x81,             // fixmap with 1 entry (0x80 | 0x01)
            0xa3,             // fixstr with length 3 (0xa0 | 0x03)
            0x6b, 0x65, 0x79, // "key" in UTF-8
            0x2a              // positive fixint 42 (0x00-0x7f range)
        };
    }

    /// <summary>
    /// 型をひととおり含む MessagePack の値の並び（REQ-188）。1 つ目はマップ（int / str / bin / float / nil / bool / 入れ子 / timestamp 32・64・96 / ext / array 16 / map 16）、
    /// 2 つ目は整数 42、3 つ目は fixext 4。Python の msgpack 1.1 の unpackb と同じ値になることを確かめた。
    /// </summary>
    public static byte[] CreateMsgpackDocument()
    {
        var ms = new MemoryStream();
        void B(params byte[] b) => ms.Write(b);
        void Str(string s) { var b = Encoding.UTF8.GetBytes(s); if (b.Length < 32) B((byte)(0xA0 | b.Length)); else if (b.Length < 256) B(0xD9, (byte)b.Length); else B(0xDA, (byte)(b.Length >> 8), (byte)b.Length); ms.Write(b); }
        B(0x8B); // fixmap 11
        Str("int"); B(0x97, 0x00, 0x7F, 0xCC, 0x80, 0xFF, 0xE0, 0xD0, 0xDF, 0xD2, 0xFF, 0xFE, 0xEE, 0x90); // [0,127,128,-1,-32,-33,-70000]
        Str("str"); B(0x93); Str(""); Str(new string('a', 31)); Str(new string('日', 100));
        Str("bin"); B(0xC4, 0x02, 0x00, 0x01);
        Str("f"); B(0x92, 0xCA, 0x3F, 0xC0, 0x00, 0x00, 0xCB, 0xC0, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00); // [1.5f, -2.25]
        Str("nil"); B(0xC0);
        Str("b"); B(0x92, 0xC3, 0xC2);
        Str("nested"); B(0x81); Str("m"); B(0x81); Str("x"); B(0x92, 0x01, 0x81); Str("y"); B(0x02);
        Str("ts32"); B(0xD6, 0xFF, 0x65, 0x53, 0xF1, 0x00); // 1700000000
        Str("ts64"); B(0xD7, 0xFF, 0x00, 0x00, 0x00, 0x14, 0x65, 0x53, 0xF1, 0x00); // ns=5, s=1700000000
        Str("ts96"); B(0xC7, 0x0C, 0xFF, 0x3B, 0x9A, 0xC9, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF); // ns=999999999, s=-1
        Str("arr16"); B(0xDC, 0x00, 0x14); for (var i = 0; i < 20; i++) B((byte)i);
        B(0x2A);
        B(0xD6, 0x01, 0x01, 0x02, 0x03, 0x04);
        return ms.ToArray();
    }

    /// <summary>
    /// 64 ビットの整数の配列（REQ-201）: [2^64 - 1, 2^63, -2^63]。Python の msgpack 1.1 の packb の出力
    /// （uint 64 の 0xCF が 2 つと int 64 の 0xD3）。
    /// </summary>
    public static byte[] CreateMsgpack64BitIntegers() =>
        Convert.FromHexString("93cfffffffffffffffffcf8000000000000000d38000000000000000");
}
