using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class Mp3ParsingTests
{
    private static readonly string Mp3FormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "mp3.bdef.yaml");

    [Fact]
    public void Mp3Format_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(Mp3FormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Mp3Format_DecodesSuccessfully()
    {
        var decoded = Decode(Mp3TestDataGenerator.CreateMinimalMp3());

        decoded.Name.Should().Be("MP3");
        decoded.Children.Select(c => c.Name).Should().Contain(["id3v2", "frames"]);
        ((DecodedVirtual)Child(decoded, "has_id3v1")).Value.Should().Be(0L);
    }

    [Fact]
    public void Mp3Format_Id3Header_DecodesCorrectly()
    {
        var id3 = Child(Decode(Mp3TestDataGenerator.CreateMinimalMp3()), "id3v2");

        var magic = (DecodedString)Child(id3, "magic");
        magic.Value.Should().Be("ID3");
        magic.Validation!.Passed.Should().BeTrue();
        ((DecodedInteger)Child(id3, "version_major")).Value.Should().Be(3);
        ((DecodedVirtual)Child(id3, "tag_size")).Value.Should().Be(12L);
    }

    [Fact]
    public void Mp3Format_Id3v24_DecodesSyncsafeFramesAndPadding()
    {
        var id3 = Child(Decode(Mp3TestDataGenerator.CreateMp3WithTags()), "id3v2");

        ((DecodedInteger)Child(id3, "version_major")).Value.Should().Be(4);
        var frames = ((DecodedArray)Child(Child(Child(id3, "body"), "frames"), "frames")).Elements;
        frames.Should().HaveCount(4); // TIT2, TPE1, TXXX, 詰め物

        ((DecodedString)Child(frames[0], "frame_id")).Value.Should().Be("TIT2");
        var title = Child(frames[0], "body");
        ((DecodedInteger)Child(title, "encoding")).EnumLabel.Should().Be("UTF-8");
        ((DecodedString)Child(Child(title, "text"), "value")).Value.Should().Be("タイトル");

        var artist = Child(Child(frames[1], "body"), "text");
        ((DecodedStruct)artist).StructType.Should().Be("utf16_bom_text");
        ((DecodedString)Child(artist, "value_le")).Value.Should().Be("Artist");

        // v2.4 のフレームの大きさはシンクセーフ整数（200 = 0x01 0x48）
        ((DecodedInteger)Child(frames[2], "frame_size_raw")).Value.Should().Be(0x148);
        ((DecodedVirtual)Child(frames[2], "frame_size")).Value.Should().Be(200L);
        ((DecodedVirtual)Child(frames[3], "is_padding")).Value.Should().Be(1L);
    }

    [Fact]
    public void Mp3Format_Frames_DecodeHeaderAndXing()
    {
        var decoded = Decode(Mp3TestDataGenerator.CreateMp3WithTags());

        var frames = ((DecodedArray)Child(decoded, "frames")).Elements;
        frames.Should().HaveCount(3);
        var first = frames[0];
        var header = (DecodedBitfield)Child(first, "header");
        header.Fields.Should().Contain(f => f.Name == "version" && f.EnumLabel == "MPEG1");
        header.Fields.Should().Contain(f => f.Name == "layer" && f.EnumLabel == "Layer_III");
        header.Fields.Should().Contain(f => f.Name == "channel_mode" && f.EnumLabel == "mono");
        ((DecodedVirtual)Child(first, "bitrate_kbps")).Value.Should().Be(128L);
        ((DecodedVirtual)Child(first, "sample_rate_hz")).Value.Should().Be(44100L);
        ((DecodedVirtual)Child(first, "frame_length")).Value.Should().Be(417L);

        var xing = Child(first, "xing_header");
        ((DecodedString)Child(xing, "tag_id")).Value.Should().Be("Info");
        ((DecodedInteger)Child(xing, "frame_count")).Value.Should().Be(2);
        ((DecodedInteger)Child(xing, "byte_count")).Value.Should().Be(417 * 3);
        ((DecodedString)Child(xing, "encoder")).Value.Should().Be("LAME3.100");

        ((DecodedStruct)frames[1]).Children.Should().NotContain(c => c.Name == "xing_header");
        frames.Select(f => f.Size).Should().AllBeEquivalentTo(417L);
    }

    [Fact]
    public void Mp3Format_Id3v1_DecodesAtEnd()
    {
        var data = Mp3TestDataGenerator.CreateMp3WithTags();
        var decoded = Decode(data);

        ((DecodedVirtual)Child(decoded, "has_id3v1")).Value.Should().Be(1L);
        var id3v1 = Child(decoded, "id3v1");
        id3v1.Offset.Should().Be(data.Length - 128);
        ((DecodedString)Child(id3v1, "title")).Value.TrimEnd('\0').Should().Be("V1Title");
        ((DecodedString)Child(id3v1, "year")).Value.Should().Be("1999");
        ((DecodedInteger)Child(id3v1, "track")).Value.Should().Be(7);
        ((DecodedInteger)Child(id3v1, "genre")).Value.Should().Be(17);
    }

    [Fact]
    public void Mp3Format_WithoutTags_DecodesMpeg2Frames()
    {
        var decoded = Decode(Mp3TestDataGenerator.CreateMp3WithoutTags());

        decoded.Children.Should().NotContain(c => c.Name == "id3v2" || c.Name == "id3v1");
        var frames = ((DecodedArray)Child(decoded, "frames")).Elements;
        frames.Should().HaveCount(2);
        ((DecodedBitfield)Child(frames[0], "header")).Fields.Should().Contain(f => f.Name == "version" && f.EnumLabel == "MPEG2");
        ((DecodedVirtual)Child(frames[0], "bitrate_kbps")).Value.Should().Be(64L);
        ((DecodedVirtual)Child(frames[0], "sample_rate_hz")).Value.Should().Be(22050L);
        ((DecodedVirtual)Child(frames[0], "frame_length")).Value.Should().Be(208L);
        frames[1].Offset.Should().Be(208);
    }

    [Fact]
    public void Mp3Format_TreeOutput_ContainsExpectedElements()
    {
        var output = new TreeOutputFormatter().Format(Decode(Mp3TestDataGenerator.CreateMinimalMp3()));

        output.Should().Contain("MP3");
        output.Should().Contain("id3v2");
        output.Should().Contain("magic");
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(Mp3FormatPath));

    private static DecodedNode Child(DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);
}
