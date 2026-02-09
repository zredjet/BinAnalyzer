using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class GzipParsingTests
{
    private static readonly string GzipFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "gzip.bdef.yaml");

    [Fact]
    public void GzipFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(GzipFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void GzipFormat_DecodesSuccessfully()
    {
        var data = GzipTestDataGenerator.CreateMinimalGzip();
        var format = new YamlFormatLoader().Load(GzipFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("GZIP");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(8);
        decoded.Children[0].Name.Should().Be("magic");
        decoded.Children[1].Name.Should().Be("compression_method");
        decoded.Children[2].Name.Should().Be("flags");
        decoded.Children[3].Name.Should().Be("mtime");
        decoded.Children[4].Name.Should().Be("xfl");
        decoded.Children[5].Name.Should().Be("os");
        decoded.Children[6].Name.Should().Be("compressed_data");
        decoded.Children[7].Name.Should().Be("crc32");
        decoded.Children[8].Name.Should().Be("isize");
    }

    [Fact]
    public void GzipFormat_Header_DecodesCorrectly()
    {
        var data = GzipTestDataGenerator.CreateMinimalGzip();
        var format = new YamlFormatLoader().Load(GzipFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var magic = decoded.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        magic.ValidationPassed.Should().BeTrue();

        var cm = decoded.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        cm.Value.Should().Be(8);
        cm.EnumLabel.Should().Be("deflate");

        // bitfieldエントリ値が正しく解析される
        var flags = decoded.Children[2].Should().BeOfType<DecodedBitfield>().Subject;
        flags.Fields.Should().Contain(f => f.Name == "FTEXT");
        flags.Fields.Should().Contain(f => f.Name == "FEXTRA");

        var os = decoded.Children[5].Should().BeOfType<DecodedInteger>().Subject;
        os.Value.Should().Be(3);
        os.EnumLabel.Should().Be("Unix");
    }

    [Fact]
    public void GzipFormat_Footer_DecodesAsIndividualFields()
    {
        var data = GzipTestDataGenerator.CreateMinimalGzip();
        var format = new YamlFormatLoader().Load(GzipFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var crc32 = decoded.Children.Should().Contain(c => c.Name == "crc32").Subject;
        crc32.Should().BeOfType<DecodedInteger>();

        var isize = decoded.Children.Should().Contain(c => c.Name == "isize").Subject;
        isize.Should().BeOfType<DecodedInteger>();
    }

    [Fact]
    public void GzipFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = GzipTestDataGenerator.CreateMinimalGzip();
        var format = new YamlFormatLoader().Load(GzipFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("GZIP");
        output.Should().Contain("magic");
        output.Should().Contain("deflate");
        output.Should().Contain("Unix");
    }
}
