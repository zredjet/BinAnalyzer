using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class ZipParsingTests
{
    private static readonly string ZipFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "zip.bdef.yaml");

    [Fact]
    public void ZipFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(ZipFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ZipFormat_DecodesMinimalZip()
    {
        var zipData = ZipTestDataGenerator.CreateMinimalZip();
        var format = new YamlFormatLoader().Load(ZipFormatPath);
        var decoded = new BinaryDecoder().Decode(zipData, format);

        decoded.Name.Should().Be("ZIP");
        var records = decoded.Children[0].Should().BeOfType<DecodedArray>().Subject;
        records.Elements.Should().HaveCount(3);
    }

    [Fact]
    public void ZipFormat_LocalFileHeader_DecodesCorrectly()
    {
        var zipData = ZipTestDataGenerator.CreateMinimalZip();
        var format = new YamlFormatLoader().Load(ZipFormatPath);
        var decoded = new BinaryDecoder().Decode(zipData, format);

        var records = decoded.Children[0].Should().BeOfType<DecodedArray>().Subject;
        var record0 = records.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        // signature
        var sig = record0.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        sig.Value.Should().Be(0x04034B50);

        // body (switch → local_file_header struct)
        var body = record0.Children[1].Should().BeOfType<DecodedStruct>().Subject;

        // compression = stored (0) — index shifted by virtual fields (version_needed_os, encrypted, has_data_descriptor, utf8_encoding)
        var compression = body.Children[6].Should().BeOfType<DecodedInteger>().Subject;
        compression.Value.Should().Be(0);
        compression.EnumLabel.Should().Be("stored");

        // filename — index shifted by virtual fields
        var filename = body.Children[14].Should().BeOfType<DecodedString>().Subject;
        filename.Value.Should().Be("hello.txt");
    }

    [Fact]
    public void ZipFormat_CentralDirectory_DecodesCorrectly()
    {
        var zipData = ZipTestDataGenerator.CreateMinimalZip();
        var format = new YamlFormatLoader().Load(ZipFormatPath);
        var decoded = new BinaryDecoder().Decode(zipData, format);

        var records = decoded.Children[0].Should().BeOfType<DecodedArray>().Subject;
        var record1 = records.Elements[1].Should().BeOfType<DecodedStruct>().Subject;

        var sig = record1.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        sig.Value.Should().Be(0x02014B50);

        var body = record1.Children[1].Should().BeOfType<DecodedStruct>().Subject;

        // filename (index 21: virtual fields for version_made_by_os, version_needed_os, encrypted, has_data_descriptor, utf8_encoding added)
        var filename = body.Children[21].Should().BeOfType<DecodedString>().Subject;
        filename.Value.Should().Be("hello.txt");
    }

    [Fact]
    public void ZipFormat_EndOfCentralDir_DecodesCorrectly()
    {
        var zipData = ZipTestDataGenerator.CreateMinimalZip();
        var format = new YamlFormatLoader().Load(ZipFormatPath);
        var decoded = new BinaryDecoder().Decode(zipData, format);

        var records = decoded.Children[0].Should().BeOfType<DecodedArray>().Subject;
        var record2 = records.Elements[2].Should().BeOfType<DecodedStruct>().Subject;

        var sig = record2.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        sig.Value.Should().Be(0x06054B50);

        var body = record2.Children[1].Should().BeOfType<DecodedStruct>().Subject;

        var totalEntries = body.Children[3].Should().BeOfType<DecodedInteger>().Subject;
        totalEntries.Value.Should().Be(1);
    }

    [Fact]
    public void ZipFormat_TreeOutput_ContainsExpectedElements()
    {
        var zipData = ZipTestDataGenerator.CreateMinimalZip();
        var format = new YamlFormatLoader().Load(ZipFormatPath);
        var decoded = new BinaryDecoder().Decode(zipData, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("ZIP");
        output.Should().Contain("records");
        output.Should().Contain("stored");
        output.Should().Contain("hello.txt");
    }
}
