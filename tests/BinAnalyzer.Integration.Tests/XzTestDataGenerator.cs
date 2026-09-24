using System.Buffers.Binary;
using System.Text;
using BinAnalyzer.Engine;

namespace BinAnalyzer.Integration.Tests;

/// <summary>
/// 展開できる .xz を組み立てる（REQ-147）。圧縮データは LZMA2 の「非圧縮チャンク」なので圧縮器は要らない。
/// Stream Header（Check = CRC32）→ Block* → Index → Stream Footer。Block Header には圧縮後サイズを書かない（xz -T1 と同じ）。
/// </summary>
public static class XzTestDataGenerator
{
    public const byte CheckCrc32 = 0x01;

    /// <summary>1 Block（"Hello, xz!"）。</summary>
    public static byte[] CreateMinimalXz() => Create(Encoding.ASCII.GetBytes("Hello, xz!"));

    /// <summary>2 Block（それぞれ 10 / 23 バイト）。</summary>
    public static byte[] CreateTwoBlockXz() =>
        Create(Encoding.ASCII.GetBytes("first blk!"), Encoding.ASCII.GetBytes("second block, a bit longer"));

    public static byte[] Create(params byte[][] blocks)
    {
        var ms = new MemoryStream();
        byte[] streamFlags = [0x00, CheckCrc32];

        // Stream Header
        ms.Write([0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00]);
        ms.Write(streamFlags);
        WriteUInt32(ms, Crc32Calculator.Compute(streamFlags));

        var records = new List<(long Unpadded, long Uncompressed)>();
        foreach (var data in blocks)
        {
            // Block Header: size, flags(1 filter, サイズなし), filter(LZMA2, props 1 byte), padding, CRC32
            byte[] headerContent = [0x00, 0x21, 0x01, 0x00];
            var headerSize = (1 + headerContent.Length + 4 + 3) / 4 * 4;
            var header = new byte[headerSize];
            header[0] = (byte)(headerSize / 4 - 1);
            headerContent.CopyTo(header, 1);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(headerSize - 4), Crc32Calculator.Compute(header.AsSpan(0, headerSize - 4)));
            ms.Write(header);

            // LZMA2: 非圧縮チャンク（辞書リセット）+ 終端
            var compressed = new List<byte> { 0x01, (byte)((data.Length - 1) >> 8), (byte)(data.Length - 1) };
            compressed.AddRange(data);
            compressed.Add(0x00);
            ms.Write(compressed.ToArray());

            var unpadded = headerSize + compressed.Count + 4;
            ms.Write(new byte[(4 - unpadded % 4) % 4]);          // Block Padding
            WriteUInt32(ms, Crc32Calculator.Compute(data));       // Check（展開後データの CRC32）
            records.Add((unpadded, data.Length));
        }

        // Index
        var index = new List<byte> { 0x00 };
        index.AddRange(Vli(records.Count));
        foreach (var (unpadded, uncompressed) in records)
        {
            index.AddRange(Vli(unpadded));
            index.AddRange(Vli(uncompressed));
        }
        while (index.Count % 4 != 0) index.Add(0x00);
        var indexBytes = index.ToArray();
        ms.Write(indexBytes);
        WriteUInt32(ms, Crc32Calculator.Compute(indexBytes));

        // Stream Footer: CRC32(Backward Size + Stream Flags), Backward Size, Stream Flags, 'YZ'
        var footer = new byte[6];
        BinaryPrimitives.WriteUInt32LittleEndian(footer, (uint)((indexBytes.Length + 4) / 4 - 1));
        streamFlags.CopyTo(footer, 4);
        WriteUInt32(ms, Crc32Calculator.Compute(footer));
        ms.Write(footer);
        ms.Write("YZ"u8);
        return ms.ToArray();
    }

    private static void WriteUInt32(Stream s, uint v)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, v);
        s.Write(b);
    }

    /// <summary>xz の可変長整数（ULEB128）。</summary>
    private static IEnumerable<byte> Vli(long v)
    {
        do
        {
            var b = (byte)(v & 0x7F);
            v >>= 7;
            yield return v != 0 ? (byte)(b | 0x80) : b;
        } while (v != 0);
    }
}
