using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.LZMA;

namespace BinAnalyzer.Compression;

/// <summary>
/// 外部NuGetパッケージを使用した圧縮データの展開処理。
/// bzip2, lzma, zstd, lz4 をサポート。
/// </summary>
public static class Decompressor
{
    /// <summary>
    /// 指定されたアルゴリズムで圧縮データを展開する。
    /// </summary>
    public static byte[] Decompress(ReadOnlyMemory<byte> compressedData, string algorithm)
    {
        return algorithm.ToLowerInvariant() switch
        {
            "bzip2" => DecompressBzip2(compressedData),
            "lzma" => DecompressLzma(compressedData),
            "zstd" or "zstandard" => DecompressZstd(compressedData),
            "lz4" => DecompressLz4(compressedData),
            _ => throw new ArgumentException($"Unsupported algorithm: {algorithm}", nameof(algorithm)),
        };
    }

    private static byte[] DecompressBzip2(ReadOnlyMemory<byte> data)
    {
        try
        {
            using var input = new MemoryStream(data.ToArray());
            using var decompressor = BZip2Stream.Create(input, CompressionMode.Decompress, decompressConcatenated: false, leaveOpen: false, tolerateTruncatedStream: false);
            using var output = new MemoryStream();
            decompressor.CopyTo(output);
            return output.ToArray();
        }
        catch (SharpCompressException ex)
        {
            // SharpCompress 固有の例外は BCL の例外に変換し、呼び出し側が外部ライブラリの型に依存しないようにする
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    private static byte[] DecompressLzma(ReadOnlyMemory<byte> data)
    {
        var span = data.Span;
        if (span.Length < 13)
            throw new InvalidDataException("LZMA data too short: need at least 13 bytes (5 properties + 8 size)");

        // raw LZMA format: 5 bytes properties + 8 bytes uncompressed size (LE) + compressed data
        var properties = span[..5].ToArray();
        var uncompressedSize = BitConverter.ToInt64(span[5..13]);
        // -1 means unknown size
        if (uncompressedSize < 0)
            uncompressedSize = -1;

        try
        {
            using var input = new MemoryStream(data[13..].ToArray());
            using var decompressor = LzmaStream.Create(properties, input, data.Length - 13, uncompressedSize, leaveOpen: false);
            using var output = new MemoryStream();
            decompressor.CopyTo(output);
            return output.ToArray();
        }
        catch (SharpCompressException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    private static byte[] DecompressZstd(ReadOnlyMemory<byte> data)
    {
        using var input = new MemoryStream(data.ToArray());
        using var decompressor = new ZstdSharp.DecompressionStream(input);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] DecompressLz4(ReadOnlyMemory<byte> data)
    {
        using var input = new MemoryStream(data.ToArray());
        using var decompressor = LZ4Stream.Decode(input);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>
    /// テスト用の圧縮ヘルパー。テストデータ生成に使用。
    /// </summary>
    public static class TestHelpers
    {
        public static byte[] Bzip2Compress(byte[] data)
        {
            using var output = new MemoryStream();
            using (var compressor = BZip2Stream.Create(output, CompressionMode.Compress, decompressConcatenated: false, leaveOpen: false, tolerateTruncatedStream: false))
            {
                compressor.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        public static byte[] LzmaCompress(byte[] data)
        {
            using var compressedStream = new MemoryStream();
            byte[] properties;
            using (var compressor = LzmaStream.Create(new LzmaEncoderProperties(), false, compressedStream))
            {
                properties = compressor.Properties;
                compressor.Write(data, 0, data.Length);
            }

            // Build raw LZMA format: 5 bytes properties + 8 bytes uncompressed size (LE) + compressed data
            var result = new MemoryStream();
            result.Write(properties);
            var sizeBytes = new byte[8];
            BitConverter.TryWriteBytes(sizeBytes, (long)data.Length);
            result.Write(sizeBytes);
            result.Write(compressedStream.ToArray());
            return result.ToArray();
        }

        public static byte[] ZstdCompress(byte[] data)
        {
            using var output = new MemoryStream();
            using (var compressor = new ZstdSharp.CompressionStream(output))
            {
                compressor.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        public static byte[] Lz4Compress(byte[] data)
        {
            using var output = new MemoryStream();
            using (var compressor = LZ4Stream.Encode(output))
            {
                compressor.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }
    }
}
