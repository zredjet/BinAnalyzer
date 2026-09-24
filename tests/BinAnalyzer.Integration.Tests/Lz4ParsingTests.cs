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
        var decoded = Decode(Lz4TestDataGenerator.CreateMinimalLz4());

        decoded.Name.Should().Be("LZ4");
        var frames = (DecodedArray)Child(decoded, "frames");
        frames.Elements.Should().HaveCount(1);
        var body = Child(frames.Elements[0], "body");
        ((DecodedStruct)body).Children.Select(c => c.Name).Should().StartWith(["flg", "bd", "header_checksum"]);
    }

    [Fact]
    public void Lz4Format_Magic_DecodesCorrectly()
    {
        var frame = Frames(Lz4TestDataGenerator.CreateMinimalLz4())[0];

        ((DecodedInteger)Child(frame, "magic")).Value.Should().Be(0x184D2204);
        var frameType = (DecodedVirtual)Child(frame, "frame_type");
        frameType.EnumLabel.Should().Be("LZ4_frame");
    }

    [Fact]
    public void Lz4Format_Bitfields_DecodeCorrectly()
    {
        var body = Child(Frames(Lz4TestDataGenerator.CreateMinimalLz4())[0], "body");

        var flg = (DecodedBitfield)Child(body, "flg");
        flg.Fields.Should().Contain(f => f.Name == "content_checksum");
        flg.Fields.Should().Contain(f => f.Name == "b_independence");
        var bd = (DecodedBitfield)Child(body, "bd");
        bd.Fields.Should().Contain(f => f.Name == "block_max_size");
    }

    [Fact]
    public void Lz4Format_Blocks_EndMarkOnly()
    {
        var blocks = (DecodedArray)Child(Child(Frames(Lz4TestDataGenerator.CreateMinimalLz4())[0], "body"), "blocks");

        // EndMark (block_size_raw=0) のみの1要素
        blocks.Elements.Should().HaveCount(1);
        var endMark = (DecodedStruct)blocks.Elements[0];
        endMark.StructType.Should().Be("lz4_data_block");
        ((DecodedInteger)Child(endMark, "block_size_raw")).Value.Should().Be(0);
        endMark.Children.Should().NotContain(c => c.Name == "block_data" || c.Name == "block_checksum");
    }

    [Fact]
    public void Lz4Format_Blocks_WithDataBlock()
    {
        var blocks = (DecodedArray)Child(Child(Frames(Lz4TestDataGenerator.CreateLz4WithDataBlock())[0], "body"), "blocks");

        // 1データブロック + EndMark = 2要素
        blocks.Elements.Should().HaveCount(2);
        var block = blocks.Elements[0];
        ((DecodedVirtual)Child(block, "block_size")).Value.Should().Be(3L);
        ((DecodedVirtual)Child(block, "is_uncompressed")).Value.Should().Be(0L);
        ((DecodedBytes)Child(block, "block_data")).RawBytes.Length.Should().Be(3);
        // FLG の b_checksum が 0 なので Block Checksum は無い
        ((DecodedStruct)block).Children.Should().NotContain(c => c.Name == "block_checksum");
    }

    [Fact]
    public void SkippableFrame_AndBlockChecksum_AreDecoded()
    {
        // 以前は Block Checksum（b_checksum = 1）を読まず、次のブロックのサイズがずれていた
        var frames = Frames(Lz4TestDataGenerator.CreateSkippableAndChecksummedFrames());
        frames.Should().HaveCount(2);

        var skippable = frames[0];
        ((DecodedVirtual)Child(skippable, "frame_type")).EnumLabel.Should().Be("skippable_frame");
        ((DecodedBytes)Child(Child(skippable, "body"), "user_data")).RawBytes.ToArray().Should().Equal("meta"u8.ToArray());

        var body = Child(frames[1], "body");
        ((DecodedInteger)Child(body, "content_size_value")).Value.Should().Be(5);
        var blocks = ((DecodedArray)Child(body, "blocks")).Elements;
        blocks.Should().HaveCount(2);
        ((DecodedVirtual)Child(blocks[0], "is_uncompressed")).Value.Should().Be(1L);
        ((DecodedBytes)Child(blocks[0], "block_data")).RawBytes.ToArray().Should().Equal("hello"u8.ToArray());
        ((DecodedInteger)Child(blocks[0], "block_checksum")).ChecksumValid.Should().BeTrue();
        ((DecodedInteger)Child(blocks[1], "block_size_raw")).Value.Should().Be(0);
    }

    [Fact]
    public void Lz4Format_TreeOutput_ContainsExpectedElements()
    {
        var output = new TreeOutputFormatter().Format(Decode(Lz4TestDataGenerator.CreateMinimalLz4()));

        output.Should().Contain("LZ4");
        output.Should().Contain("magic");
        output.Should().Contain("flg");
        output.Should().Contain("bd");
        output.Should().Contain("blocks");
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(Lz4FormatPath));

    private static IReadOnlyList<DecodedNode> Frames(byte[] data) => ((DecodedArray)Child(Decode(data), "frames")).Elements;

    private static DecodedNode Child(DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);
}
