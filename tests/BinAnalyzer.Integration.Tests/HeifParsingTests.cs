using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class HeifParsingTests
{
    private static readonly string HeifFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "heif.bdef.yaml");

    [Fact]
    public void HeifFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(HeifFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void HeifFormat_DecodesSuccessfully()
    {
        var heifData = HeifTestDataGenerator.CreateMinimalHeif();
        var format = new YamlFormatLoader().Load(HeifFormatPath);
        var decoded = new BinaryDecoder().Decode(heifData, format);

        decoded.Name.Should().Be("HEIF");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(1);
        decoded.Children[0].Name.Should().Be("boxes");
    }

    [Fact]
    public void HeifFormat_FtypBox_DecodesCorrectly()
    {
        var heifData = HeifTestDataGenerator.CreateMinimalHeif();
        var format = new YamlFormatLoader().Load(HeifFormatPath);
        var decoded = new BinaryDecoder().Decode(heifData, format);

        var boxes = decoded.Children[0].Should().BeOfType<DecodedArray>().Subject;
        boxes.Elements.Should().HaveCountGreaterThanOrEqualTo(1);

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
        majorBrand.Value.Should().Be("heic");

        var minorVersion = boxData.Children.First(c => c.Name == "minor_version")
            .Should().BeOfType<DecodedInteger>().Subject;
        minorVersion.Value.Should().Be(0);
    }

    [Fact]
    public void HeifFormat_MetaBox_DecodesCorrectly()
    {
        var heifData = HeifTestDataGenerator.CreateMinimalHeif();
        var format = new YamlFormatLoader().Load(HeifFormatPath);
        var decoded = new BinaryDecoder().Decode(heifData, format);

        var boxes = decoded.Children[0].Should().BeOfType<DecodedArray>().Subject;

        // boxes[1] = meta iso_box
        var metaBox = boxes.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        var metaType = metaBox.Children.First(c => c.Name == "box_type")
            .Should().BeOfType<DecodedString>().Subject;
        metaType.Value.Should().Be("meta");

        // meta → meta_box → children
        var metaData = metaBox.Children.First(c => c.Name == "box_data")
            .Should().BeOfType<DecodedStruct>().Subject;
        var metaChildren = metaData.Children.First(c => c.Name == "children")
            .Should().BeOfType<DecodedArray>().Subject;

        // hdlr should be the first child
        var hdlrBox = metaChildren.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var hdlrType = hdlrBox.Children.First(c => c.Name == "box_type")
            .Should().BeOfType<DecodedString>().Subject;
        hdlrType.Value.Should().Be("hdlr");

        // hdlr → hdlr_box → handler_type
        var hdlrData = hdlrBox.Children.First(c => c.Name == "box_data")
            .Should().BeOfType<DecodedStruct>().Subject;
        var handlerType = hdlrData.Children.First(c => c.Name == "handler_type")
            .Should().BeOfType<DecodedString>().Subject;
        handlerType.Value.Should().Be("pict");
    }

    [Fact]
    public void HeifFormat_TreeOutput_ContainsExpectedElements()
    {
        var heifData = HeifTestDataGenerator.CreateMinimalHeif();
        var format = new YamlFormatLoader().Load(HeifFormatPath);
        var decoded = new BinaryDecoder().Decode(heifData, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("HEIF");
        output.Should().Contain("ftyp");
        output.Should().Contain("heic");
        output.Should().Contain("meta");
        output.Should().Contain("hdlr");
        output.Should().Contain("pict");
    }
}
