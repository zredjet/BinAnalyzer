using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class GifParsingTests
{
    private static readonly string GifFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "gif.bdef.yaml");

    [Fact]
    public void GifFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(GifFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void GifFormat_DecodesWithRecovery()
    {
        var data = GifTestDataGenerator.CreateMinimalGif();
        var format = new YamlFormatLoader().Load(GifFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        decoded.Name.Should().Be("GIF");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(3);
        decoded.Children[0].Name.Should().Be("signature");
        decoded.Children[1].Name.Should().Be("version");
        decoded.Children[2].Name.Should().Be("logical_screen_desc");
    }

    [Fact]
    public void GifFormat_Header_DecodesCorrectly()
    {
        var data = GifTestDataGenerator.CreateMinimalGif();
        var format = new YamlFormatLoader().Load(GifFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var sig = decoded.Children[0].Should().BeOfType<DecodedString>().Subject;
        sig.Value.Should().Be("GIF");

        var version = decoded.Children[1].Should().BeOfType<DecodedString>().Subject;
        version.Value.Should().Be("89a");

        var lsd = decoded.Children[2].Should().BeOfType<DecodedStruct>().Subject;
        var width = lsd.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        width.Value.Should().Be(1);
    }

    [Fact]
    public void GifFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = GifTestDataGenerator.CreateMinimalGif();
        var format = new YamlFormatLoader().Load(GifFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var output = new TreeOutputFormatter().Format(result.Root);

        output.Should().Contain("GIF");
        output.Should().Contain("signature");
        output.Should().Contain("logical_screen_desc");
    }
}
