using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class IccParsingTests
{
    private static readonly string IccFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "icc.bdef.yaml");

    [Fact]
    public void IccFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(IccFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void IccFormat_DecodesMinimalIcc()
    {
        var data = IccTestDataGenerator.CreateMinimalIcc();
        var format = new YamlFormatLoader().Load(IccFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("ICC");
        decoded.Children.Should().HaveCount(2);
        decoded.Children[0].Name.Should().Be("header");
        decoded.Children[1].Name.Should().Be("tag_table");
    }

    [Fact]
    public void IccFormat_Header_DecodesCorrectly()
    {
        var data = IccTestDataGenerator.CreateMinimalIcc();
        var format = new YamlFormatLoader().Load(IccFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var header = decoded.Children[0].Should().BeOfType<DecodedStruct>().Subject;

        var profileSize = header.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        profileSize.Name.Should().Be("profile_size");
        profileSize.Value.Should().Be(132);

        var deviceClass = header.Children[3].Should().BeOfType<DecodedInteger>().Subject;
        deviceClass.Name.Should().Be("device_class");
        deviceClass.EnumLabel.Should().Be("Monitor");

        var colorSpace = header.Children[4].Should().BeOfType<DecodedInteger>().Subject;
        colorSpace.Name.Should().Be("color_space");
        colorSpace.EnumLabel.Should().Be("RGB");

        var signature = header.Children[7].Should().BeOfType<DecodedString>().Subject;
        signature.Name.Should().Be("signature");
        signature.Value.Should().Be("acsp");

        var renderingIntent = header.Children[13].Should().BeOfType<DecodedInteger>().Subject;
        renderingIntent.Name.Should().Be("rendering_intent");
        renderingIntent.EnumLabel.Should().Be("Perceptual");
    }

    [Fact]
    public void IccFormat_DescTag_DecodesCorrectly()
    {
        var data = IccTestDataGenerator.CreateIccWithTags();
        var format = new YamlFormatLoader().Load(IccFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var tagTable = decoded.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var tags = tagTable.Children[1].Should().BeOfType<DecodedArray>().Subject;
        tags.Elements.Should().HaveCount(2);

        // tags[0] = desc tag_entry
        var descEntry = tags.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var sig = descEntry.Children[0].Should().BeOfType<DecodedString>().Subject;
        sig.Value.Should().Be("desc");

        // data → switch → desc_tag_data
        var descData = descEntry.Children[3].Should().BeOfType<DecodedStruct>().Subject;
        var typeSig = descData.Children[0].Should().BeOfType<DecodedString>().Subject;
        typeSig.Value.Should().Be("desc");

        var asciiLength = descData.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        asciiLength.Value.Should().Be(12);

        var asciiDesc = descData.Children[3].Should().BeOfType<DecodedString>().Subject;
        asciiDesc.Value.Should().Be("Test Profile");
    }

    [Fact]
    public void IccFormat_XyzTag_DecodesCorrectly()
    {
        var data = IccTestDataGenerator.CreateIccWithTags();
        var format = new YamlFormatLoader().Load(IccFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var tagTable = decoded.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var tags = tagTable.Children[1].Should().BeOfType<DecodedArray>().Subject;

        // tags[1] = XYZ  tag_entry
        var xyzEntry = tags.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        var sig = xyzEntry.Children[0].Should().BeOfType<DecodedString>().Subject;
        sig.Value.Should().Be("XYZ ");

        // data → switch → xyz_tag_data
        var xyzData = xyzEntry.Children[3].Should().BeOfType<DecodedStruct>().Subject;
        var typeSig = xyzData.Children[0].Should().BeOfType<DecodedString>().Subject;
        typeSig.Value.Should().Be("XYZ ");

        var x = xyzData.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        x.Name.Should().Be("x");
        x.Value.Should().Be(0x0000F6D6);

        var y = xyzData.Children[3].Should().BeOfType<DecodedInteger>().Subject;
        y.Name.Should().Be("y");
        y.Value.Should().Be(0x00010000);
    }

    [Fact]
    public void IccFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = IccTestDataGenerator.CreateMinimalIcc();
        var format = new YamlFormatLoader().Load(IccFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("ICC");
        output.Should().Contain("header");
        output.Should().Contain("Monitor");
        output.Should().Contain("RGB");
        output.Should().Contain("Perceptual");
        output.Should().Contain("tag_table");
    }
}
