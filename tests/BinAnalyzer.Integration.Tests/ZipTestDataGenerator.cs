using System.Buffers.Binary;

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
}
