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
        var decoded = Decode(FlvTestDataGenerator.CreateMinimalFlv());

        decoded.Name.Should().Be("FLV");
        decoded.Children.Select(c => c.Name).Should().Equal("signature", "version", "flags", "data_offset", "prev_tag_size_0", "tags");
        Child(decoded, "prev_tag_size_0").Validation!.Passed.Should().BeTrue();
    }

    [Fact]
    public void FlvFormat_Signature_DecodesCorrectly()
    {
        ((DecodedString)Child(Decode(FlvTestDataGenerator.CreateMinimalFlv()), "signature")).Value.Should().Be("FLV");
    }

    [Fact]
    public void FlvFormat_Header_DecodesCorrectly()
    {
        var decoded = Decode(FlvTestDataGenerator.CreateMinimalFlv());

        ((DecodedInteger)Child(decoded, "version")).Value.Should().Be(1);
        ((DecodedInteger)Child(decoded, "data_offset")).Value.Should().Be(9);
    }

    [Fact]
    public void FlvFormat_AudioTagHeader_DecodesCorrectly()
    {
        var decoded = Decode(FlvTestDataGenerator.CreateMinimalFlv());

        ((DecodedArray)Child(decoded, "tags")).Elements.Should().HaveCount(1);
        var tag = Tag(decoded, 0);
        ((DecodedBitfield)Child(tag, "tag_header")).Fields.Should().Contain(f => f.Name == "tag_type" && f.EnumLabel == "audio");
        var audioHeader = (DecodedBitfield)Child(Child(tag, "data"), "audio_header");
        // 0xAA = 1010_1010 → sound_format = 10（AAC）、sound_rate = 2（22 kHz）、sound_size = 1（16 ビット）、sound_type = 0（モノラル）
        audioHeader.Fields.Should().Contain(f => f.Name == "sound_format" && f.Value == 10);
        audioHeader.Fields.Should().Contain(f => f.Name == "sound_rate" && f.Value == 2);
        audioHeader.Fields.Should().Contain(f => f.Name == "sound_size" && f.Value == 1);
        audioHeader.Fields.Should().Contain(f => f.Name == "sound_type" && f.Value == 0);
        // 中身が 1 バイトなので AACPacketType は無い
        ((DecodedStruct)Child(tag, "data")).Children.Should().NotContain(c => c.Name == "aac_packet_type");
    }

    [Fact]
    public void FlvFormat_VideoTagHeader_DecodesCorrectly()
    {
        var tag = Tag(Decode(FlvTestDataGenerator.CreateFlvWithVideoTag()), 0);

        var videoHeader = (DecodedBitfield)Child(Child(tag, "data"), "video_header");
        // 0x17 = 0001_0111 → frame_type = 1（キーフレーム）、codec_id = 7（AVC / H.264）
        videoHeader.Fields.Should().Contain(f => f.Name == "frame_type" && f.Value == 1);
        videoHeader.Fields.Should().Contain(f => f.Name == "codec_id" && f.Value == 7);
    }

    [Fact]
    public void FlvFormat_ScriptTag_DecodesOnMetaData()
    {
        var tag = Tag(Decode(FlvTestDataGenerator.CreateFlvWithMetadata()), 0);

        var values = ((DecodedArray)Child(Child(tag, "data"), "values")).Elements;
        values.Should().HaveCount(2);
        ((DecodedString)Child(Child(values[0], "value"), "text")).Value.Should().Be("onMetaData");
        var array = Child(values[1], "value");
        ((DecodedStruct)array).StructType.Should().Be("amf0_ecma_array");
        ((DecodedInteger)Child(array, "count")).Value.Should().Be(4);

        var properties = ((DecodedArray)Child(array, "properties")).Elements;
        properties.Should().HaveCount(5); // 4 個 + 終わりの印
        PropertyValue(properties[0], "duration").Should().BeOfType<DecodedFloat>().Which.Value.Should().Be(1.5);
        PropertyValue(properties[1], "width").Should().BeOfType<DecodedFloat>().Which.Value.Should().Be(64);
        PropertyValue(properties[2], "encoder").Should().BeOfType<DecodedString>().Which.Value.Should().Be("gen");
        PropertyValue(properties[3], "stereo").Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(1);
        ((DecodedInteger)Child(Child(properties[4], "property_value"), "amf_type")).EnumLabel.Should().Be("object_end");
    }

    [Fact]
    public void FlvFormat_AvcTags_DecodePacketTypeAndCompositionTime()
    {
        var decoded = Decode(FlvTestDataGenerator.CreateFlvWithMetadata());

        var sequenceHeader = Child(Tag(decoded, 1), "data");
        ((DecodedInteger)Child(sequenceHeader, "avc_packet_type")).EnumLabel.Should().Be("sequence_header");

        var nalu = Tag(decoded, 2);
        ((DecodedVirtual)Child(nalu, "timestamp_ms")).Value.Should().Be(40L);
        var naluData = Child(nalu, "data");
        ((DecodedInteger)Child(naluData, "avc_packet_type")).EnumLabel.Should().Be("NALU");
        ((DecodedVirtual)Child(naluData, "composition_time_ms")).Value.Should().Be(-40L);
        Child(naluData, "video_data").Size.Should().Be(5);
    }

    [Fact]
    public void FlvFormat_AacTag_DecodesPacketType()
    {
        var audio = Child(Tag(Decode(FlvTestDataGenerator.CreateFlvWithMetadata()), 3), "data");

        ((DecodedBitfield)Child(audio, "audio_header")).Fields.Should().Contain(f => f.Name == "sound_format" && f.EnumLabel == "AAC");
        ((DecodedInteger)Child(audio, "aac_packet_type")).EnumLabel.Should().Be("sequence_header");
        Child(audio, "audio_data").Size.Should().Be(2);
    }

    [Fact]
    public void FlvFormat_PreviousTagSize_IsValidated()
    {
        var tags = ((DecodedArray)Child(Decode(FlvTestDataGenerator.CreateFlvWithMetadata()), "tags")).Elements;

        tags.Should().HaveCount(4);
        tags.Select(t => Child(t, "prev_tag_size").Validation!.Passed).Should().AllBeEquivalentTo(true);
    }

    [Fact]
    public void FlvFormat_TreeOutput_ContainsExpectedElements()
    {
        var output = new TreeOutputFormatter().Format(Decode(FlvTestDataGenerator.CreateMinimalFlv()));

        output.Should().Contain("FLV");
        output.Should().Contain("signature");
        output.Should().Contain("data_offset");
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(FlvFormatPath));

    private static DecodedNode Tag(DecodedStruct root, int index) =>
        ((DecodedArray)Child(root, "tags")).Elements[index];

    /// <summary>AMF0 のプロパティの名前を確かめ、値（amf0_value の中身の最後の子。文字列なら長さの後ろの本文）を返す。</summary>
    private static DecodedNode PropertyValue(DecodedNode property, string name)
    {
        ((DecodedString)Child(property, "name")).Value.Should().Be(name);
        return ((DecodedStruct)Child(Child(property, "property_value"), "value")).Children[^1];
    }

    private static DecodedNode Child(DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);
}
