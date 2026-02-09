using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class Lz4ParsingTests
{
    private static readonly string Lz4FormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "lz4.bdef.yaml");

    [Fact]
    public void Lz4Format_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(Lz4FormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Lz4Format_DecodesSuccessfully()
    {
        var data = Lz4TestDataGenerator.CreateMinimalLz4();
        var format = new YamlFormatLoader().Load(Lz4FormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("LZ4");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(4);
        decoded.Children[0].Name.Should().Be("magic");
        decoded.Children[1].Name.Should().Be("flg");
        decoded.Children[2].Name.Should().Be("bd");
    }

    [Fact]
    public void Lz4Format_Magic_DecodesCorrectly()
    {
        var data = Lz4TestDataGenerator.CreateMinimalLz4();
        var format = new YamlFormatLoader().Load(Lz4FormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var magic = decoded.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        magic.Value.Should().Be(0x184D2204);
    }

    [Fact]
    public void Lz4Format_Bitfields_DecodeCorrectly()
    {
        var data = Lz4TestDataGenerator.CreateMinimalLz4();
        var format = new YamlFormatLoader().Load(Lz4FormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var flg = decoded.Children[1].Should().BeOfType<DecodedBitfield>().Subject;
        flg.Name.Should().Be("flg");
        flg.Fields.Should().Contain(f => f.Name == "content_checksum");
        flg.Fields.Should().Contain(f => f.Name == "b_independence");

        var bd = decoded.Children[2].Should().BeOfType<DecodedBitfield>().Subject;
        bd.Name.Should().Be("bd");
        bd.Fields.Should().Contain(f => f.Name == "block_max_size");
    }

    [Fact]
    public void Lz4Format_Blocks_EndMarkOnly()
    {
        var data = Lz4TestDataGenerator.CreateMinimalLz4();
        var format = new YamlFormatLoader().Load(Lz4FormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        // blocks は header_checksum の後にある
        var blocks = decoded.Children.FirstOrDefault(c => c.Name == "blocks");
        blocks.Should().NotBeNull();
        var blocksArray = blocks.Should().BeOfType<DecodedArray>().Subject;
        // EndMark (block_size_raw=0) のみの1要素
        blocksArray.Elements.Should().HaveCount(1);

        var endMark = blocksArray.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        endMark.StructType.Should().Be("lz4_data_block");
        var blockSizeRaw = endMark.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        blockSizeRaw.Name.Should().Be("block_size_raw");
        blockSizeRaw.Value.Should().Be(0);
    }

    [Fact]
    public void Lz4Format_Blocks_WithDataBlock()
    {
        var data = Lz4TestDataGenerator.CreateLz4WithDataBlock();
        var format = new YamlFormatLoader().Load(Lz4FormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var blocks = decoded.Children.FirstOrDefault(c => c.Name == "blocks");
        blocks.Should().NotBeNull();
        var blocksArray = blocks.Should().BeOfType<DecodedArray>().Subject;
        // 1データブロック + EndMark = 2要素
        blocksArray.Elements.Should().HaveCount(2);

        // 最初のブロック: block_size_raw > 0, block_data あり
        var block1 = blocksArray.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var size1 = block1.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        size1.Value.Should().Be(3);
        block1.Children.Should().HaveCount(2); // block_size_raw + block_data

        // EndMark: block_size_raw == 0, block_data なし
        var endMark = blocksArray.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        var sizeEnd = endMark.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        sizeEnd.Value.Should().Be(0);
        endMark.Children.Should().HaveCount(1); // block_size_raw のみ
    }

    [Fact]
    public void Lz4Format_TreeOutput_ContainsExpectedElements()
    {
        var data = Lz4TestDataGenerator.CreateMinimalLz4();
        var format = new YamlFormatLoader().Load(Lz4FormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("LZ4");
        output.Should().Contain("magic");
        output.Should().Contain("flg");
        output.Should().Contain("bd");
        output.Should().Contain("blocks");
    }
}
