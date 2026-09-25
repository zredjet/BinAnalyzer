using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class FlacParsingTests
{
    private static readonly string FlacFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "flac.bdef.yaml");

    [Fact]
    public void FlacFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(FlacFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void FlacFormat_DecodesSuccessfully()
    {
        var decoded = Decode(FlacTestDataGenerator.CreateMinimalFlac());

        decoded.Name.Should().Be("FLAC");
        // 音声フレームが無いので first_frame_header は無い
        decoded.Children.Select(c => c.Name).Should().Equal("file_size", "magic", "metadata_blocks", "frames");
        Child(decoded, "frames").Size.Should().Be(0);
    }

    [Fact]
    public void FlacFormat_Magic_DecodesCorrectly()
    {
        var magic = (DecodedBytes)Child(Decode(FlacTestDataGenerator.CreateMinimalFlac()), "magic");

        magic.ValidationPassed.Should().BeTrue();
    }

    [Fact]
    public void FlacFormat_StreamInfo_BitfieldDecodesCorrectly()
    {
        var block = Block(Decode(FlacTestDataGenerator.CreateMinimalFlac()), 0);

        var header = (DecodedBitfield)Child(block, "header");
        header.Fields.Should().Contain(f => f.Name == "is_last" && f.Value == 1);
        header.Fields.Should().Contain(f => f.Name == "block_type" && f.EnumLabel == "STREAMINFO");
        header.Fields.Should().Contain(f => f.Name == "length" && f.Value == 34);

        var streamInfo = Child(block, "data");
        var bitfield = (DecodedBitfield)Child(streamInfo, "sample_rate_channels_bps_samples");
        bitfield.Fields.Should().Contain(f => f.Name == "sample_rate" && f.Value == 44100);
        bitfield.Fields.Should().Contain(f => f.Name == "channels" && f.Value == 1); // 2 チャンネルを 1 で表す
        bitfield.Fields.Should().Contain(f => f.Name == "bps" && f.Value == 15); // 16 ビットを 15 で表す
        bitfield.Fields.Should().Contain(f => f.Name == "total_samples" && f.Value == 0);
        ((DecodedVirtual)Child(streamInfo, "channel_count")).Value.Should().Be(2L);
        ((DecodedVirtual)Child(streamInfo, "bits_per_sample")).Value.Should().Be(16L);
    }

    [Fact]
    public void FlacFormat_CuesheetIndex_DecodesCorrectly()
    {
        var decoded = Decode(FlacTestDataGenerator.CreateFlacWithCuesheet());

        ((DecodedArray)Child(decoded, "metadata_blocks")).Elements.Should().HaveCount(2);
        var cuesheet = Child(Block(decoded, 1), "data");
        var tracks = (DecodedArray)Child(cuesheet, "tracks");
        tracks.Elements.Should().HaveCount(1);

        var track = tracks.Elements[0];
        ((DecodedInteger)Child(track, "num_indices")).Value.Should().Be(1);
        var index = ((DecodedArray)Child(track, "indices")).Elements.Single();
        ((DecodedInteger)Child(index, "offset")).Value.Should().Be(0);
        ((DecodedInteger)Child(index, "index_number")).Value.Should().Be(1);
        Child(index, "reserved").Should().BeOfType<DecodedBytes>();
    }

    [Fact]
    public void FlacFormat_FirstFrameHeader_DecodesCorrectly()
    {
        var decoded = Decode(FlacTestDataGenerator.CreateFlacWithFrameHeader());

        var frameHeader = Child(decoded, "first_frame_header");
        var header = (DecodedBitfield)Child(frameHeader, "header");
        header.Fields.Should().Contain(f => f.Name == "sync_code" && f.Value == 0x3FFE);
        header.Fields.Should().Contain(f => f.Name == "blocking_strategy" && f.Value == 0);
        header.Fields.Should().Contain(f => f.Name == "block_size_code" && f.Value == 12);
        header.Fields.Should().Contain(f => f.Name == "sample_rate_code" && f.Value == 9);
        header.Fields.Should().Contain(f => f.Name == "channel_code" && f.EnumLabel == "left_side");
        header.Fields.Should().Contain(f => f.Name == "sample_size_code" && f.Value == 4);
        ((DecodedVirtual)Child(frameHeader, "sync_valid")).Value.Should().Be(1L);
        // 先読みなので frames は最初のフレームの先頭から始まる
        Child(decoded, "frames").Offset.Should().Be(frameHeader.Offset);
        Child(decoded, "frames").Size.Should().Be(8);
    }

    [Fact]
    public void FlacFormat_TreeOutput_ContainsExpectedElements()
    {
        var output = new TreeOutputFormatter().Format(Decode(FlacTestDataGenerator.CreateMinimalFlac()));

        output.Should().Contain("FLAC");
        output.Should().Contain("magic");
        output.Should().Contain("metadata_blocks");
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(FlacFormatPath));

    private static DecodedNode Block(DecodedStruct root, int index) =>
        ((DecodedArray)Child(root, "metadata_blocks")).Elements[index];

    private static DecodedNode Child(DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);
}
