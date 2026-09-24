using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

/// <summary>REQ-147: XZ。</summary>
public class XzParsingTests
{
    private static readonly string XzFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "xz.bdef.yaml");

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(XzFormatPath));

    private static T At<T>(DecodedStruct root, string path) where T : DecodedNode
    {
        DecodedNode node = root;
        foreach (var part in path.Split('.'))
        {
            node = part.StartsWith('#')
                ? ((DecodedArray)node).Elements[int.Parse(part[1..])]
                : ((DecodedStruct)node).Children.Single(c => c.Name == part);
        }
        return (T)node;
    }

    [Fact]
    public void XzFormat_LoadsWithoutDiagnostics()
    {
        var format = new YamlFormatLoader().Load(XzFormatPath);
        FormatValidator.Validate(format).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void MinimalXz_AllCrc32ChecksumsAndMagicsAreValid()
    {
        var root = Decode(XzTestDataGenerator.CreateMinimalXz());

        At<DecodedBytes>(root, "stream_header.magic").ValidationPassed.Should().BeTrue();
        At<DecodedInteger>(root, "stream_header.crc32").ChecksumValid.Should().BeTrue();
        At<DecodedInteger>(root, "stream_footer.crc32").ChecksumValid.Should().BeTrue();
        At<DecodedBytes>(root, "stream_footer.magic").ValidationPassed.Should().BeTrue();
        At<DecodedInteger>(root, "index.crc32").ChecksumValid.Should().BeTrue();
        At<DecodedInteger>(root, "blocks.#0.header.crc32").ChecksumValid.Should().BeTrue();
        At<DecodedInteger>(root, "stream_header.stream_flags.check_type").EnumLabel.Should().Be("CRC32");
    }

    [Fact]
    public void MinimalXz_BlockSizesComeFromTheIndex()
    {
        var root = Decode(XzTestDataGenerator.CreateMinimalXz());

        At<DecodedInteger>(root, "index.body.record_count").Value.Should().Be(1);
        var flags = At<DecodedBitfield>(root, "blocks.#0.header.body.flags");
        flags.Fields.Single(f => f.Name == "has_compressed_size").Value.Should().Be(0, "xz -T1 と同じくサイズを書かない");
        At<DecodedStruct>(root, "blocks.#0.header.body").Children.Should().NotContain(c => c.Name == "compressed_size");
    }

    [Fact]
    public void MinimalXz_CompressedDataAndCheck()
    {
        var root = Decode(XzTestDataGenerator.CreateMinimalXz());

        // Block Header に圧縮後サイズは無い。Index の Unpadded Size（12 + 14 + 4 = 30）から 14 バイトを求める
        var compressed = At<DecodedBytes>(root, "blocks.#0.compressed_data");
        compressed.Size.Should().Be(14, "LZMA2 非圧縮チャンク: 制御 1 + サイズ 2 + データ 10 + 終端 1");
        compressed.RawBytes.Span[0].Should().Be(0x01);
        At<DecodedBytes>(root, "blocks.#0.check").Size.Should().Be(4);
        At<DecodedInteger>(root, "blocks.#0.header.body.filters.#0.filter_id").EnumLabel.Should().Be("LZMA2");
    }

    [Fact]
    public void TwoBlockXz_DecodesBothBlocks()
    {
        var data = XzTestDataGenerator.CreateTwoBlockXz();
        var root = Decode(data);

        var blocks = At<DecodedArray>(root, "blocks");
        blocks.Elements.Should().HaveCount(2);
        At<DecodedBytes>(root, "blocks.#1.compressed_data").Size.Should().Be(3 + 26 + 1);
        // 2 つ目の Block の終わり（Check の後ろ）がちょうど Index の先頭
        var check1 = At<DecodedBytes>(root, "blocks.#1.check");
        (check1.Offset + check1.Size).Should().Be(At<DecodedStruct>(root, "index").Offset);
        At<DecodedInteger>(root, "blocks.#1.header.crc32").ChecksumValid.Should().BeTrue();
    }
}
