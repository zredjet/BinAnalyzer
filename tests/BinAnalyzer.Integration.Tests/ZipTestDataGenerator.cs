using BinAnalyzer.Engine;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class ZipTestDataGenerator
{
    /// <summary>
    /// 1ファイル（"hello.txt", 内容 "Hello", 無圧縮）の最小ZIPを生成する。
    /// Local file header(30+9+5=44) + Central directory(46+9=55) + EOCD(22) = 121バイト
    /// </summary>
    public static byte[] CreateMinimalZip()
    {
        var filename = "hello.txt"u8;
        var fileData = "Hello"u8;
        var crc = BinAnalyzer.Engine.Crc32Calculator.Compute(fileData);

        // サイズ計算
        var localHeaderSize = 30 + filename.Length + fileData.Length; // 44
        var centralDirSize = 46 + filename.Length;                    // 55
        var eocdSize = 22;
        var totalSize = localHeaderSize + centralDirSize + eocdSize;

        var data = new byte[totalSize];
        var span = data.AsSpan();
        var pos = 0;

        // === Local file header ===
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x04034B50); pos += 4; // signature
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 20);         pos += 2; // version needed
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0);          pos += 2; // flags
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0);          pos += 2; // compression = stored
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0);          pos += 2; // mod_time
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0);          pos += 2; // mod_date
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], crc);        pos += 4; // crc32
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], (uint)fileData.Length); pos += 4; // compressed_size
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], (uint)fileData.Length); pos += 4; // uncompressed_size
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], (ushort)filename.Length); pos += 2; // filename_length
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0);          pos += 2; // extra_length
        filename.CopyTo(span[pos..]);                                       pos += filename.Length; // filename
        fileData.CopyTo(span[pos..]);                                       pos += fileData.Length; // data

        var centralDirOffset = pos;

        // === Central directory entry ===
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x02014B50); pos += 4; // signature
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 20);         pos += 2; // version made by
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 20);         pos += 2; // version needed
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0);          pos += 2; // flags
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0);          pos += 2; // compression = stored
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0);          pos += 2; // mod_time
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0);          pos += 2; // mod_date
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], crc);        pos += 4; // crc32
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], (uint)fileData.Length); pos += 4; // compressed_size
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], (uint)fileData.Length); pos += 4; // uncompressed_size
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], (ushort)filename.Length); pos += 2; // filename_length
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0);          pos += 2; // extra_length
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0);          pos += 2; // comment_length
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0);          pos += 2; // disk_number_start
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0);          pos += 2; // internal_attributes
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0);          pos += 4; // external_attributes
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0);          pos += 4; // local_header_offset
        filename.CopyTo(span[pos..]);                                       pos += filename.Length; // filename

        // === End of central directory ===
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x06054B50); pos += 4; // signature
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0);          pos += 2; // disk_number
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0);          pos += 2; // start_disk
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 1);          pos += 2; // entries_on_disk
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 1);          pos += 2; // total_entries
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], (uint)centralDirSize); pos += 4; // directory_size
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], (uint)centralDirOffset); pos += 4; // directory_offset
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0);          pos += 2; // comment_length

        return data;
    }

    /// <summary>
    /// .NET の ZipArchive でシークできないストリームに書いた ZIP（REQ-188）。deflate の 2 エントリで、
    /// どちらも Local File Header のサイズが 0 で、データの後ろに署名付きの Data Descriptor がある。
    /// </summary>
    public static byte[] CreateStreamedDeflateZip()
    {
        var output = new MemoryStream();
        using (var stream = new NonSeekableStream(output))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, text) in new[] { ("dd.txt", string.Concat(Enumerable.Repeat("descriptor ", 20))), ("second.txt", "second") })
            {
                using var entry = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
                entry.Write(Encoding.ASCII.GetBytes(text));
            }
        }
        return output.ToArray();
    }

    /// <summary>
    /// Zip64 の 1 エントリ（無圧縮 "zip64!"、汎用フラグ bit 3）の ZIP（REQ-188）。Data Descriptor は署名なしで 8 バイトのサイズ、
    /// Central Directory のサイズは 0xFFFFFFFF で Zip64 拡張フィールドに実際の値、末尾は Zip64 End of Central Directory Record + Locator +
    /// コメント付きの End of Central Directory。
    /// </summary>
    public static byte[] CreateZip64WithUnsignedDataDescriptor()
    {
        var content = Encoding.ASCII.GetBytes("zip64!");
        var name = Encoding.ASCII.GetBytes("big.txt");
        var crc = (uint)ChecksumCalculators.Integer["crc32"](content);
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);

        // Local File Header（サイズは 0、Zip64 拡張フィールドも 0）
        w.Write(0x04034B50u); w.Write((ushort)45); w.Write((ushort)0x0008); w.Write((ushort)0);
        w.Write((ushort)0x6000); w.Write((ushort)0x5D38); w.Write(0u); w.Write(0xFFFFFFFFu); w.Write(0xFFFFFFFFu);
        w.Write((ushort)name.Length); w.Write((ushort)20); w.Write(name);
        w.Write((ushort)0x0001); w.Write((ushort)16); w.Write(0UL); w.Write(0UL);
        w.Write(content);
        // Data Descriptor（署名なし、Zip64 なので 8 バイトのサイズ）
        w.Write(crc); w.Write((ulong)content.Length); w.Write((ulong)content.Length);

        // Central Directory
        var cdOffset = ms.Position;
        w.Write(0x02014B50u); w.Write((ushort)(3 << 8 | 45)); w.Write((ushort)45); w.Write((ushort)0x0008); w.Write((ushort)0);
        w.Write((ushort)0x6000); w.Write((ushort)0x5D38); w.Write(crc); w.Write(0xFFFFFFFFu); w.Write(0xFFFFFFFFu);
        w.Write((ushort)name.Length); w.Write((ushort)20); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0);
        w.Write(0x81A4u << 16); w.Write(0u); w.Write(name);
        w.Write((ushort)0x0001); w.Write((ushort)16); w.Write((ulong)content.Length); w.Write((ulong)content.Length);
        var cdSize = ms.Position - cdOffset;

        // Zip64 End of Central Directory Record + Locator
        var zip64EocdOffset = ms.Position;
        w.Write(0x06064B50u); w.Write(44UL); w.Write((ushort)(3 << 8 | 45)); w.Write((ushort)45); w.Write(0u); w.Write(0u);
        w.Write(1UL); w.Write(1UL); w.Write((ulong)cdSize); w.Write((ulong)cdOffset);
        w.Write(0x07064B50u); w.Write(0u); w.Write((ulong)zip64EocdOffset); w.Write(1u);

        // End of Central Directory（値は Zip64 の印、コメント付き）
        var comment = Encoding.ASCII.GetBytes("comment");
        w.Write(0x06054B50u); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0xFFFF); w.Write((ushort)0xFFFF);
        w.Write(0xFFFFFFFFu); w.Write(0xFFFFFFFFu); w.Write((ushort)comment.Length); w.Write(comment);
        return ms.ToArray();
    }

    /// <summary>書き込み専用でシークできないストリーム（ZipArchive に Data Descriptor を書かせるため）。</summary>
    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
    }
}
