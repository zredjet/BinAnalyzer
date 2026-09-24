using BinAnalyzer.Engine;
using System.Buffers.Binary;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class SevenZipTestDataGenerator
{
    /// <summary>
    /// 最小7zファイル: signature_header(32B) + next_header(2B) = 34バイト
    /// 6Bシグネチャ + version(2B) + start_header_crc(4B) + next_header_offset(8B) + next_header_size(8B) + next_header_crc(4B) + header_data(2B)
    /// </summary>
    public static byte[] CreateMinimal7z()
    {
        var data = new byte[34];
        var span = data.AsSpan();
        var pos = 0;

        // === signature_header (32 bytes) ===
        // signature: 37 7A BC AF 27 1C
        data[0] = 0x37;
        data[1] = 0x7A;
        data[2] = 0xBC;
        data[3] = 0xAF;
        data[4] = 0x27;
        data[5] = 0x1C;
        pos = 6;

        // major_version: 0
        data[pos] = 0; pos += 1;

        // minor_version: 4
        data[pos] = 4; pos += 1;

        // start_header_crc: 0 (dummy)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;

        // next_header_offset: 0 (relative to end of signature header = byte 32)
        BinaryPrimitives.WriteUInt64LittleEndian(span[pos..], 0); pos += 8;

        // next_header_size: 2
        BinaryPrimitives.WriteUInt64LittleEndian(span[pos..], 2); pos += 8;

        // next_header_crc: 0 (dummy)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;

        // === next_header (at offset 32, 2 bytes) ===
        // header_data: 2 dummy bytes
        data[pos] = 0x01; pos += 1; // Header property ID
        data[pos] = 0x00;           // end marker

        return data;
    }

    /// <summary>
    /// 無圧縮（Copy）のソリッドなフォルダ 1 つに "a.txt"・"b.txt" を入れた 7z（REQ-188）。
    /// <paramref name="encodeHeader"/> が true なら、ヘッダ自体も Copy の Packed Stream に置いて kEncodedHeader で指す（7-Zip の既定の形）。
    /// ファイル一覧には 2 バイトの可変長整数を使う kDummy（200 バイト）を入れる。
    /// </summary>
    public static byte[] Create7zWithCopyFolder(bool encodeHeader)
    {
        var a = Encoding.ASCII.GetBytes("hello\n");
        var b = Encoding.ASCII.GetBytes("world!\n");
        var files = a.Concat(b).ToArray();
        var crc32 = ChecksumCalculators.Integer["crc32"];

        var h = new MemoryStream();
        h.WriteByte(0x01);                                   // kHeader
        h.WriteByte(0x04);                                   // kMainStreamsInfo
        h.Write([0x06, 0x00, 0x01, 0x09, (byte)files.Length, 0x00]);  // kPackInfo: pos 0, 1 stream, kSize, kEnd
        h.Write([0x07, 0x0B, 0x01, 0x00, 0x01, 0x01, 0x00, 0x0C, (byte)files.Length, 0x00]); // kUnPackInfo: 1 folder, Copy
        h.Write([0x08, 0x0D, 0x02, 0x09, (byte)a.Length, 0x0A, 0x01]); // kSubStreamsInfo: 2 streams, size of a, CRCs
        WriteUInt32(h, (uint)crc32(a));
        WriteUInt32(h, (uint)crc32(b));
        h.Write([0x00, 0x00]);                               // kEnd (SubStreams), kEnd (StreamsInfo)
        h.Write([0x05, 0x02]);                               // kFilesInfo: 2 files
        var names = Encoding.Unicode.GetBytes("a.txt\0b.txt\0");
        h.Write([0x11, (byte)(names.Length + 1), 0x00]);     // kName, size, external = 0
        h.Write(names);
        h.Write([0x19, 0x80, 0xC8]);                         // kDummy, size 200（2 バイトの可変長整数）
        h.Write(new byte[200]);
        h.Write([0x14, 1 + 1 + 16, 0x01, 0x00]);             // kMTime: all defined, external = 0
        WriteUInt64(h, 133_000_000_000_000_000UL);
        WriteUInt64(h, 133_000_000_010_000_000UL);
        h.Write([0x15, 1 + 1 + 8, 0x01, 0x00]);              // kWinAttributes
        WriteUInt32(h, 0x20);
        WriteUInt32(h, 0x20);
        h.Write([0x00, 0x00]);                               // kEnd (FilesInfo), kEnd (Header)
        var plainHeader = h.ToArray();

        byte[] packed, nextHeader;
        if (encodeHeader)
        {
            packed = files.Concat(plainHeader).ToArray();
            var e = new MemoryStream();
            e.WriteByte(0x17);                               // kEncodedHeader
            e.Write([0x06, (byte)files.Length, 0x01, 0x09]); // kPackInfo: pos = ファイルの後ろ, 1 stream, kSize
            WriteNumber(e, plainHeader.Length);
            e.WriteByte(0x00);
            e.Write([0x07, 0x0B, 0x01, 0x00, 0x01, 0x01, 0x00, 0x0C]);
            WriteNumber(e, plainHeader.Length);
            e.Write([0x0A, 0x01]);                           // フォルダの CRC
            WriteUInt32(e, (uint)crc32(plainHeader));
            e.Write([0x00, 0x00]);
            nextHeader = e.ToArray();
        }
        else
        {
            packed = files;
            nextHeader = plainHeader;
        }

        var data = new byte[32 + packed.Length + nextHeader.Length];
        var span = data.AsSpan();
        new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C, 0x00, 0x04 }.CopyTo(span);
        BinaryPrimitives.WriteUInt64LittleEndian(span[12..], (ulong)packed.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(span[20..], (ulong)nextHeader.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..], (uint)crc32(nextHeader));
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], (uint)crc32(span[12..32]));
        packed.CopyTo(span[32..]);
        nextHeader.CopyTo(span[(32 + packed.Length)..]);
        return data;
    }

    /// <summary>7z の可変長整数（0x4000 未満だけ対応）。</summary>
    private static void WriteNumber(Stream s, int value)
    {
        if (value < 0x80)
        {
            s.WriteByte((byte)value);
            return;
        }
        s.WriteByte((byte)(0x80 | (value >> 8)));
        s.WriteByte((byte)value);
    }

    private static void WriteUInt32(Stream s, uint value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, value);
        s.Write(buf);
    }

    private static void WriteUInt64(Stream s, ulong value)
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(buf, value);
        s.Write(buf);
    }
}
