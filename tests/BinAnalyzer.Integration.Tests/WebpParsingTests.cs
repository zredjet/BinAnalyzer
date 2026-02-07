using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class WebpParsingTests
{
    private static readonly string WebpFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "webp.bdef.yaml");

    [Fact]
    public void WebpFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(WebpFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void WebpFormat_DecodesWithRecovery()
    {
        var data = WebpTestDataGenerator.CreateMinimalWebp();
        var format = new YamlFormatLoader().Load(WebpFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        decoded.Name.Should().Be("WebP");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(3);
        decoded.Children[0].Name.Should().Be("riff_magic");
        decoded.Children[1].Name.Should().Be("file_size");
        decoded.Children[2].Name.Should().Be("webp_magic");
    }

    [Fact]
    public void WebpFormat_Magic_DecodesCorrectly()
    {
        var data = WebpTestDataGenerator.CreateMinimalWebp();
        var format = new YamlFormatLoader().Load(WebpFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var riffMagic = decoded.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        riffMagic.ValidationPassed.Should().BeTrue();

        var webpMagic = decoded.Children[2].Should().BeOfType<DecodedBytes>().Subject;
        webpMagic.ValidationPassed.Should().BeTrue();
    }

    [Fact]
    public void WebpFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = WebpTestDataGenerator.CreateMinimalWebp();
        var format = new YamlFormatLoader().Load(WebpFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var output = new TreeOutputFormatter().Format(result.Root);

        output.Should().Contain("WebP");
        output.Should().Contain("riff_magic");
        output.Should().Contain("webp_magic");
    }
}
