using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class Mp4ParsingTests
{
    private static readonly string Mp4FormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "mp4.bdef.yaml");

    [Fact]
    public void Mp4Format_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(Mp4FormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Mp4Format_DecodesSuccessfully()
    {
        var mp4Data = Mp4TestDataGenerator.CreateMinimalMp4();
        var format = new YamlFormatLoader().Load(Mp4FormatPath);
        var decoded = new BinaryDecoder().Decode(mp4Data, format);

        decoded.Name.Should().Be("MP4");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(1);
        decoded.Children[0].Name.Should().Be("boxes");
    }

    [Fact]
    public void Mp4Format_FtypBox_DecodesCorrectly()
    {
        var mp4Data = Mp4TestDataGenerator.CreateMinimalMp4();
        var format = new YamlFormatLoader().Load(Mp4FormatPath);
        var decoded = new BinaryDecoder().Decode(mp4Data, format);

        var boxes = decoded.Children[0].Should().BeOfType<DecodedArray>().Subject;
        boxes.Elements.Should().HaveCountGreaterThanOrEqualTo(2);

        // boxes[0] = ftyp iso_box
        var ftypBox = boxes.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        var boxType = ftypBox.Children.First(c => c.Name == "box_type")
            .Should().BeOfType<DecodedString>().Subject;
        boxType.Value.Should().Be("ftyp");

        // box_data → ftyp_box
        var boxData = ftypBox.Children.First(c => c.Name == "box_data")
            .Should().BeOfType<DecodedStruct>().Subject;

        var majorBrand = boxData.Children.First(c => c.Name == "major_brand")
            .Should().BeOfType<DecodedString>().Subject;
        majorBrand.Value.Should().Be("isom");

        var minorVersion = boxData.Children.First(c => c.Name == "minor_version")
            .Should().BeOfType<DecodedInteger>().Subject;
        minorVersion.Value.Should().Be(0x200);
    }

    [Fact]
    public void Mp4Format_ContainerBoxes_Decoded()
    {
        var mp4Data = Mp4TestDataGenerator.CreateMinimalMp4();
        var format = new YamlFormatLoader().Load(Mp4FormatPath);
        var decoded = new BinaryDecoder().Decode(mp4Data, format);

        var boxes = decoded.Children[0].Should().BeOfType<DecodedArray>().Subject;

        // boxes[1] = moov iso_box
        var moovBox = boxes.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        var moovType = moovBox.Children.First(c => c.Name == "box_type")
            .Should().BeOfType<DecodedString>().Subject;
        moovType.Value.Should().Be("moov");

        // moov → container_box → children
        var moovData = moovBox.Children.First(c => c.Name == "box_data")
            .Should().BeOfType<DecodedStruct>().Subject;
        var moovChildren = moovData.Children[0].Should().BeOfType<DecodedArray>().Subject;
        moovChildren.Elements.Should().HaveCountGreaterThanOrEqualTo(2); // mvhd + trak

        // children[1] = trak
        var trakBox = moovChildren.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        var trakType = trakBox.Children.First(c => c.Name == "box_type")
            .Should().BeOfType<DecodedString>().Subject;
        trakType.Value.Should().Be("trak");
    }

    [Fact]
    public void Mp4Format_TreeOutput_ContainsExpectedElements()
    {
        var mp4Data = Mp4TestDataGenerator.CreateMinimalMp4();
        var format = new YamlFormatLoader().Load(Mp4FormatPath);
        var decoded = new BinaryDecoder().Decode(mp4Data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("MP4");
        output.Should().Contain("ftyp");
        output.Should().Contain("isom");
        output.Should().Contain("moov");
        output.Should().Contain("trak");
        output.Should().Contain("soun");
    }
}
