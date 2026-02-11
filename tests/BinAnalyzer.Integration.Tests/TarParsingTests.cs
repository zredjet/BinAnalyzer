using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class TarParsingTests
{
    private static readonly string TarFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "tar.bdef.yaml");

    [Fact]
    public void TarFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(TarFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void TarFormat_DecodesMinimalTar()
    {
        var data = TarTestDataGenerator.CreateMinimalTar();
        var format = new YamlFormatLoader().Load(TarFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("TAR");
        decoded.Children.Should().HaveCount(1);
        decoded.Children[0].Name.Should().Be("entries");
    }

    [Fact]
    public void TarFormat_Header_DecodesCorrectly()
    {
        var data = TarTestDataGenerator.CreateMinimalTar();
        var format = new YamlFormatLoader().Load(TarFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var entries = decoded.Children[0].Should().BeOfType<DecodedArray>().Subject;
        entries.Elements.Should().HaveCount(1);

        var header = entries.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        var name = header.Children[0].Should().BeOfType<DecodedString>().Subject;
        name.Name.Should().Be("name");
        name.Value.Should().Contain("hello.txt");

        var typeflag = header.Children[7].Should().BeOfType<DecodedInteger>().Subject;
        typeflag.Name.Should().Be("typeflag");
        typeflag.Value.Should().Be(0x30);
        typeflag.EnumLabel.Should().Be("regular_file");
    }

    [Fact]
    public void TarFormat_Magic_ValidatesCorrectly()
    {
        var data = TarTestDataGenerator.CreateMinimalTar();
        var format = new YamlFormatLoader().Load(TarFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var entries = decoded.Children[0].Should().BeOfType<DecodedArray>().Subject;
        var header = entries.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        var magic = header.Children[9].Should().BeOfType<DecodedString>().Subject;
        magic.Name.Should().Be("magic");
        magic.Value.Should().Contain("ustar");
    }

    [Fact]
    public void TarFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = TarTestDataGenerator.CreateMinimalTar();
        var format = new YamlFormatLoader().Load(TarFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("TAR");
        output.Should().Contain("entries");
        output.Should().Contain("hello.txt");
        output.Should().Contain("regular_file");
        output.Should().Contain("uname");
    }

    [Fact]
    public void TarFormat_VirtualFields_ComputeCorrectly()
    {
        var data = TarTestDataGenerator.CreateMinimalTar();
        var format = new YamlFormatLoader().Load(TarFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var entries = decoded.Children[0].Should().BeOfType<DecodedArray>().Subject;
        entries.Elements.Should().HaveCount(1);

        var entry = entries.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        // file_size_bytes = parse_int("00000000000\0", 8) = 0
        var fileSizeBytes = entry.Children.First(c => c.Name == "file_size_bytes")
            .Should().BeOfType<DecodedVirtual>().Subject;
        fileSizeBytes.Value.Should().Be(0L);

        // data_blocks_size = ((0 + 511) / 512) * 512 = 0
        var dataBlocksSize = entry.Children.First(c => c.Name == "data_blocks_size")
            .Should().BeOfType<DecodedVirtual>().Subject;
        dataBlocksSize.Value.Should().Be(0L);

        // data field should be skipped (condition: file_size_bytes > 0 is false)
        entry.Children.Should().NotContain(c => c.Name == "data");
    }
}
