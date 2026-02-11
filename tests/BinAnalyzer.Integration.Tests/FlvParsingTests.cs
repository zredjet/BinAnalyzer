using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class FlvParsingTests
{
    private static readonly string FlvFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "flv.bdef.yaml");

    [Fact]
    public void FlvFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(FlvFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void FlvFormat_DecodesSuccessfully()
    {
        var data = FlvTestDataGenerator.CreateMinimalFlv();
        var format = new YamlFormatLoader().Load(FlvFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("FLV");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(5);
        decoded.Children[0].Name.Should().Be("signature");
        decoded.Children[1].Name.Should().Be("version");
        decoded.Children[2].Name.Should().Be("flags");
        decoded.Children[3].Name.Should().Be("data_offset");
        decoded.Children[4].Name.Should().Be("prev_tag_size_0");
    }

    [Fact]
    public void FlvFormat_Signature_DecodesCorrectly()
    {
        var data = FlvTestDataGenerator.CreateMinimalFlv();
        var format = new YamlFormatLoader().Load(FlvFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var sig = decoded.Children[0].Should().BeOfType<DecodedString>().Subject;
        sig.Value.Should().Be("FLV");
    }

    [Fact]
    public void FlvFormat_Header_DecodesCorrectly()
    {
        var data = FlvTestDataGenerator.CreateMinimalFlv();
        var format = new YamlFormatLoader().Load(FlvFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var version = decoded.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        version.Value.Should().Be(1);

        var dataOffset = decoded.Children[3].Should().BeOfType<DecodedInteger>().Subject;
        dataOffset.Value.Should().Be(9);
    }

    [Fact]
    public void FlvFormat_AudioTagHeader_DecodesCorrectly()
    {
        var data = FlvTestDataGenerator.CreateMinimalFlv();
        var format = new YamlFormatLoader().Load(FlvFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        // Navigate: tags[0].data (switch → audio_tag_data) → audio_header bitfield
        var tags = decoded.Children[5].Should().BeOfType<DecodedArray>().Subject;
        tags.Elements.Should().HaveCount(1);

        var tag = tags.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        // flv_tag: tag_type(0), data_size_b0(1), data_size_b1(2), data_size_b2(3), data_size(4,virtual),
        //   timestamp_b0(5), timestamp_b1(6), timestamp_b2(7), timestamp_ext(8), stream_id(9), data(10), prev_tag_size(11)
        var tagData = tag.Children[10].Should().BeOfType<DecodedStruct>().Subject; // data (switch → audio_tag_data)

        // audio_tag_data: audio_header, audio_data
        var audioHeader = tagData.Children[0].Should().BeOfType<DecodedBitfield>().Subject;
        audioHeader.Name.Should().Be("audio_header");

        // 0xAA = 1010_1010 → sound_format=10(AAC), sound_rate=2(22kHz), sound_size=1(16bit), sound_type=0(mono)
        audioHeader.Fields.Should().Contain(f => f.Name == "sound_format" && f.Value == 10);
        audioHeader.Fields.Should().Contain(f => f.Name == "sound_rate" && f.Value == 2);
        audioHeader.Fields.Should().Contain(f => f.Name == "sound_size" && f.Value == 1);
        audioHeader.Fields.Should().Contain(f => f.Name == "sound_type" && f.Value == 0);
    }

    [Fact]
    public void FlvFormat_VideoTagHeader_DecodesCorrectly()
    {
        var data = FlvTestDataGenerator.CreateFlvWithVideoTag();
        var format = new YamlFormatLoader().Load(FlvFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var tags = decoded.Children[5].Should().BeOfType<DecodedArray>().Subject;
        tags.Elements.Should().HaveCount(1);

        var tag = tags.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var tagData = tag.Children[10].Should().BeOfType<DecodedStruct>().Subject;

        var videoHeader = tagData.Children[0].Should().BeOfType<DecodedBitfield>().Subject;
        videoHeader.Name.Should().Be("video_header");

        // 0x17 = 0001_0111 → frame_type=1(key), codec_id=7(AVC/H.264)
        videoHeader.Fields.Should().Contain(f => f.Name == "frame_type" && f.Value == 1);
        videoHeader.Fields.Should().Contain(f => f.Name == "codec_id" && f.Value == 7);
    }

    [Fact]
    public void FlvFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = FlvTestDataGenerator.CreateMinimalFlv();
        var format = new YamlFormatLoader().Load(FlvFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("FLV");
        output.Should().Contain("signature");
        output.Should().Contain("data_offset");
    }
}
