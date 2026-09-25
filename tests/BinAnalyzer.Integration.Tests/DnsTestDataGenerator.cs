using System.Buffers.Binary;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class DnsTestDataGenerator
{
    /// <summary>
    /// 最小の DNS の問い合わせ（33 バイト、REQ-188 で正しい形に直した）: ヘッダ（ID 0x1234、再帰の要求、質問 1）+ 質問（www.example.com、A、IN）。
    /// 以前は質問の部分が 4 バイトで、クラスの途中で終わっていた
    /// </summary>
    public static byte[] CreateMinimalDns() =>
    [
        0x12, 0x34, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        3, (byte)'w', (byte)'w', (byte)'w', 7, (byte)'e', (byte)'x', (byte)'a', (byte)'m', (byte)'p', (byte)'l', (byte)'e', 3, (byte)'c', (byte)'o', (byte)'m', 0,
        0x00, 0x01, 0x00, 0x01,
    ];

    /// <summary>
    /// 圧縮ポインタを使う応答（REQ-188）。質問 www.example.com A、回答 6 件（A・AAAA・MX・TXT・CNAME・CAA）、権威 1 件（SOA）、
    /// 追加 1 件（EDNS の OPT、DO ビット・COOKIE のオプション）。名前は質問の www.example.com（位置 12）と example.com（位置 16）を指すポインタで書く。
    /// </summary>
    public static byte[] CreateDnsResponse()
    {
        var ms = new MemoryStream();
        void U16(int v) { ms.WriteByte((byte)(v >> 8)); ms.WriteByte((byte)v); }
        void U32(uint v) { U16((int)(v >> 16)); U16((int)(v & 0xFFFF)); }
        void Bytes(params byte[] b) => ms.Write(b);
        void Label(string s) { ms.WriteByte((byte)s.Length); ms.Write(Encoding.ASCII.GetBytes(s)); }
        const int PtrWww = 0xC00C, PtrExample = 0xC010;
        void Rr(Action name, int type, byte[] rdata)
        {
            name();
            U16(type); U16(1); U32(300); U16(rdata.Length); ms.Write(rdata);
        }
        static byte[] B(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
        static byte[] Be16(int v) => [(byte)(v >> 8), (byte)v];
        static byte[] L(string s) => new[] { (byte)s.Length }.Concat(Encoding.ASCII.GetBytes(s)).ToArray();

        U16(0x1234); U16(0x8180); U16(1); U16(6); U16(1); U16(1);
        Label("www"); Label("example"); Label("com"); ms.WriteByte(0); U16(1); U16(1);
        Rr(() => U16(PtrWww), 1, [93, 184, 216, 34]);
        Rr(() => U16(PtrWww), 28, Convert.FromHexString("26062800022000010248189325C81946"));
        Rr(() => U16(PtrExample), 15, B(Be16(10), L("mail"), Be16(PtrExample)));
        Rr(() => U16(PtrExample), 16, B(L("v=spf1 -all"), L("hello")));
        Rr(() => { Label("cdn"); U16(PtrExample); }, 5, B(L("www"), Be16(PtrExample)));
        Rr(() => U16(PtrExample), 257, B([0], L("issue"), Encoding.ASCII.GetBytes("letsencrypt.org")));
        Rr(() => U16(PtrExample), 6, B(L("ns"), Be16(PtrExample), L("admin"), Be16(PtrExample),
            [0x78, 0xC3, 0xB7, 0xD5, 0, 0, 0x1C, 0x20, 0, 0, 0x0E, 0x10, 0, 0x12, 0x75, 0, 0, 0, 1, 0x2C]));
        Bytes(0); U16(41); U16(1232); Bytes(0, 0); U16(0x8000); U16(12); U16(10); U16(8); Bytes(1, 2, 3, 4, 5, 6, 7, 8);
        return ms.ToArray();
    }
}
