using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
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
    public void GzipFormat_DecodesWithRecovery()
    {
        var data = GzipTestDataGenerator.CreateMinimalGzip();
        var format = new YamlFormatLoader().Load(GzipFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        decoded.Name.Should().Be("GZIP");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(6);
        decoded.Children[0].Name.Should().Be("magic");
        decoded.Children[1].Name.Should().Be("compression_method");
        decoded.Children[2].Name.Should().Be("flags");
        decoded.Children[3].Name.Should().Be("mtime");
        decoded.Children[4].Name.Should().Be("xfl");
        decoded.Children[5].Name.Should().Be("os");
    }

    [Fact]
    public void GzipFormat_Header_DecodesCorrectly()
    {
        var data = GzipTestDataGenerator.CreateMinimalGzip();
        var format = new YamlFormatLoader().Load(GzipFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var magic = decoded.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        magic.ValidationPassed.Should().BeTrue();

        var cm = decoded.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        cm.Value.Should().Be(8);
        cm.EnumLabel.Should().Be("deflate");

        var os = decoded.Children[5].Should().BeOfType<DecodedInteger>().Subject;
        os.Value.Should().Be(3);
        os.EnumLabel.Should().Be("Unix");
    }

    [Fact]
    public void GzipFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = GzipTestDataGenerator.CreateMinimalGzip();
        var format = new YamlFormatLoader().Load(GzipFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var output = new TreeOutputFormatter().Format(result.Root);

        output.Should().Contain("GZIP");
        output.Should().Contain("magic");
        output.Should().Contain("deflate");
        output.Should().Contain("Unix");
    }
}
