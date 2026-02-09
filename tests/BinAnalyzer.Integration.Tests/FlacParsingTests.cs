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
        var data = FlacTestDataGenerator.CreateMinimalFlac();
        var format = new YamlFormatLoader().Load(FlacFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("FLAC");
        decoded.Children.Should().HaveCount(2);
        decoded.Children[0].Name.Should().Be("magic");
        decoded.Children[1].Name.Should().Be("metadata_blocks");
    }

    [Fact]
    public void FlacFormat_Magic_DecodesCorrectly()
    {
        var data = FlacTestDataGenerator.CreateMinimalFlac();
        var format = new YamlFormatLoader().Load(FlacFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var magic = decoded.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        magic.ValidationPassed.Should().BeTrue();
    }

    [Fact]
    public void FlacFormat_StreamInfo_BitfieldDecodesCorrectly()
    {
        var data = FlacTestDataGenerator.CreateMinimalFlac();
        var format = new YamlFormatLoader().Load(FlacFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        // Navigate: metadata_blocks[0].data (switch → streaminfo) → sample_rate_channels_bps_samples
        var metadataBlocks = (DecodedArray)decoded.Children[1]; // metadata_blocks (repeat → array)
        var firstBlock = (DecodedStruct)metadataBlocks.Elements[0]; // first metadata_block
        // Fields: header_byte, is_last, block_type, length_b0, length_b1, length_b2, length, data
        var streamInfoSwitch = (DecodedStruct)firstBlock.Children[7]; // data (switch → streaminfo)
        // streaminfo: min_block_size, max_block_size, min_frame_size_b0..b2, min_frame_size,
        //   max_frame_size_b0..b2, max_frame_size, sample_rate_channels_bps_samples, md5
        var bitfield = streamInfoSwitch.Children[10].Should().BeOfType<DecodedBitfield>().Subject;

        bitfield.Name.Should().Be("sample_rate_channels_bps_samples");
        bitfield.Fields.Should().Contain(f => f.Name == "sample_rate" && f.Value == 44100);
        bitfield.Fields.Should().Contain(f => f.Name == "channels" && f.Value == 1); // stereo = 2ch, stored as 1
        bitfield.Fields.Should().Contain(f => f.Name == "bps" && f.Value == 15); // 16-bit, stored as 15
        bitfield.Fields.Should().Contain(f => f.Name == "total_samples" && f.Value == 0);
    }

    [Fact]
    public void FlacFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = FlacTestDataGenerator.CreateMinimalFlac();
        var format = new YamlFormatLoader().Load(FlacFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("FLAC");
        output.Should().Contain("magic");
        output.Should().Contain("metadata_blocks");
    }
}
