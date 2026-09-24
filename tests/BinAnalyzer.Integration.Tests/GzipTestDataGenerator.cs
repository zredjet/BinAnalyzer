using BinAnalyzer.Engine;
using System.IO.Compression;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class GzipTestDataGenerator
{
    /// <summary>
    /// 最小gzipファイル: magic(2B) + compression_method(1B) + flags(1B) + mtime(4B) + xfl(1B) + os(1B) +
    /// compressed_data(10B: empty deflate stream + CRC32 + ISIZE) = 20バイト
    /// FNAME=0, FEXTRA=0, FHCRC=0, FCOMMENT=0
    /// </summary>
    public static byte[] CreateMinimalGzip()
    {
        var data = new byte[20];
        var pos = 0;

        // magic: 0x1F 0x8B
        data[pos] = 0x1F; pos += 1;
        data[pos] = 0x8B; pos += 1;

        // compression_method: 8 (deflate)
        data[pos] = 0x08; pos += 1;

        // flags: 0 (no FTEXT, FHCRC, FEXTRA, FNAME, FCOMMENT)
        data[pos] = 0x00; pos += 1;

        // mtime: 0 (4 bytes LE)
        pos += 4;

        // xfl: 2 (maximum compression)
        data[pos] = 0x02; pos += 1;

        // os: 3 (Unix)
        data[pos] = 0x03; pos += 1;

        // compressed_data (remaining): empty deflate block + CRC32 + ISIZE
        // Minimal empty deflate: 0x03 0x00 (final block, no data)
        data[pos] = 0x03; pos += 1;
        data[pos] = 0x00; pos += 1;

        // CRC32 of empty input: 0x00000000 (4 bytes LE)
        pos += 4;

        // ISIZE of empty input: 0x00000000 (4 bytes LE)
        // pos += 4; (already zeroed)

        return data;
    }

    /// <summary>
    /// FEXTRA（BGZF と同じ 'BC' サブフィールド）・FNAME・FCOMMENT・FHCRC をすべて持つ gzip（REQ-188）。内容は "hello gzip\n" × 3。
    /// </summary>
    public static byte[] CreateGzipWithOptionalFields()
    {
        var content = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("hello gzip\n", 3)));
        var deflated = new MemoryStream();
        using (var deflate = new DeflateStream(deflated, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(content);

        var header = new MemoryStream();
        var w = new BinaryWriter(header);
        w.Write((byte)0x1F); w.Write((byte)0x8B); w.Write((byte)8);
        w.Write((byte)0b0001_1110);          // FHCRC | FEXTRA | FNAME | FCOMMENT
        w.Write(1_700_000_000u);             // MTIME
        w.Write((byte)0); w.Write((byte)3);  // XFL, OS = Unix
        const int extraLength = 6;
        w.Write((ushort)extraLength);
        w.Write((byte)'B'); w.Write((byte)'C'); w.Write((ushort)2);
        // BSIZE − 1（メンバー全体のバイト数 − 1）
        var memberSize = 10 + 2 + extraLength + "hello.txt\0".Length + "note\0".Length + 2 + (int)deflated.Length + 8;
        w.Write((ushort)(memberSize - 1));
        w.Write(Encoding.ASCII.GetBytes("hello.txt\0"));
        w.Write(Encoding.ASCII.GetBytes("note\0"));
        w.Write((ushort)ChecksumCalculators.Integer["crc32"](header.ToArray()));
        w.Write(deflated.ToArray());
        w.Write((uint)ChecksumCalculators.Integer["crc32"](content));
        w.Write((uint)content.Length);
        return header.ToArray();
    }
}
