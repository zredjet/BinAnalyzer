using BinAnalyzer.Compression;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Compression.Tests;

public class DecompressorTests
{
    private static readonly byte[] TestData = { 0x01, 0x02, 0x03, 0x04, 0x05 };

    [Fact]
    public void DecompressBzip2_KnownData_ReturnsExpected()
    {
        var compressed = Decompressor.TestHelpers.Bzip2Compress(TestData);

        var result = Decompressor.Decompress(compressed, "bzip2");

        result.Should().BeEquivalentTo(TestData);
    }

    [Fact]
    public void DecompressLzma_KnownData_ReturnsExpected()
    {
        var compressed = Decompressor.TestHelpers.LzmaCompress(TestData);

        var result = Decompressor.Decompress(compressed, "lzma");

        result.Should().BeEquivalentTo(TestData);
    }

    [Fact]
    public void DecompressZstd_KnownData_ReturnsExpected()
    {
        var compressed = Decompressor.TestHelpers.ZstdCompress(TestData);

        var result = Decompressor.Decompress(compressed, "zstd");

        result.Should().BeEquivalentTo(TestData);
    }

    [Fact]
    public void DecompressLz4_KnownData_ReturnsExpected()
    {
        var compressed = Decompressor.TestHelpers.Lz4Compress(TestData);

        var result = Decompressor.Decompress(compressed, "lz4");

        result.Should().BeEquivalentTo(TestData);
    }

    [Fact]
    public void DecompressZstandard_Alias_ReturnsExpected()
    {
        var compressed = Decompressor.TestHelpers.ZstdCompress(TestData);

        var result = Decompressor.Decompress(compressed, "zstandard");

        result.Should().BeEquivalentTo(TestData);
    }

    [Fact]
    public void DecompressBzip2_InvalidData_ThrowsException()
    {
        var invalidData = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };

        var act = () => Decompressor.Decompress(invalidData, "bzip2");

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void DecompressLzma_InvalidData_ThrowsException()
    {
        var invalidData = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };

        var act = () => Decompressor.Decompress(invalidData, "lzma");

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void DecompressZstd_InvalidData_ThrowsException()
    {
        var invalidData = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };

        var act = () => Decompressor.Decompress(invalidData, "zstd");

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void DecompressLz4_InvalidData_ThrowsException()
    {
        var invalidData = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };

        var act = () => Decompressor.Decompress(invalidData, "lz4");

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Decompress_UnsupportedAlgorithm_ThrowsArgumentException()
    {
        var data = new byte[] { 0x01 };

        var act = () => Decompressor.Decompress(data, "unknown");

        act.Should().Throw<ArgumentException>();
    }
}
