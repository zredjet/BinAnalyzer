using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class OtfParsingTests
{
    private static readonly string OtfFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "otf.bdef.yaml");

    [Fact]
    public void OtfFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(OtfFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void OtfFormat_DecodesMinimalOtf()
    {
        var data = OtfTestDataGenerator.CreateMinimalOtf();
        var format = new YamlFormatLoader().Load(OtfFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("OTF");
        decoded.Children.Should().HaveCount(2);
        decoded.Children[0].Name.Should().Be("offset_table");
        decoded.Children[1].Name.Should().Be("table_records");
    }

    [Fact]
    public void OtfFormat_OffsetTable_DecodesCorrectly()
    {
        var data = OtfTestDataGenerator.CreateMinimalOtf();
        var format = new YamlFormatLoader().Load(OtfFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var offsetTable = decoded.Children[0].Should().BeOfType<DecodedStruct>().Subject;

        var sfVersion = offsetTable.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        sfVersion.Name.Should().Be("sfVersion");
        sfVersion.EnumLabel.Should().Be("CFF");

        var numTables = offsetTable.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        numTables.Value.Should().Be(1);
    }

    [Fact]
    public void OtfFormat_HeadTable_DecodesCorrectly()
    {
        var data = OtfTestDataGenerator.CreateMinimalOtf();
        var format = new YamlFormatLoader().Load(OtfFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var records = decoded.Children[1].Should().BeOfType<DecodedArray>().Subject;
        records.Elements.Should().HaveCount(1);

        var record = records.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var tag = record.Children[0].Should().BeOfType<DecodedString>().Subject;
        tag.Value.Should().Be("head");
    }

    [Fact]
    public void OtfFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = OtfTestDataGenerator.CreateMinimalOtf();
        var format = new YamlFormatLoader().Load(OtfFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("OTF");
        output.Should().Contain("offset_table");
        output.Should().Contain("CFF");
        output.Should().Contain("head");
    }
}
