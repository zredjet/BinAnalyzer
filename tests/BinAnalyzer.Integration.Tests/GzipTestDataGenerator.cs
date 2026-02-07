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
}
