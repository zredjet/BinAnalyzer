using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class TreeOutputTests
{
    private static readonly string PngFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "png.bdef.yaml");

    [Fact]
    public void TreeOutput_MinimalPng_ContainsExpectedElements()
    {
        var pngData = PngTestDataGenerator.CreateMinimalPng();
        var format = new YamlFormatLoader().Load(PngFormatPath);
        var decoded = new BinaryDecoder().Decode(pngData, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("PNG");
        output.Should().Contain("signature");
        output.Should().Contain("✓"); // valid signature
        output.Should().Contain("chunks");
        output.Should().Contain("IHDR");
        output.Should().Contain("width: 1");
        output.Should().Contain("height: 1");
        output.Should().Contain("truecolor");
        output.Should().Contain("IEND");
    }

    [Fact]
    public void TreeOutput_MinimalPng_ContainsOffsets()
    {
        var pngData = PngTestDataGenerator.CreateMinimalPng();
        var format = new YamlFormatLoader().Load(PngFormatPath);
        var decoded = new BinaryDecoder().Decode(pngData, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("[0x00000000]"); // root offset
        output.Should().Contain("[0x00000008]"); // first chunk after 8-byte signature
    }

    [Fact]
    public void TreeOutput_MinimalPng_ContainsTreeCharacters()
    {
        var pngData = PngTestDataGenerator.CreateMinimalPng();
        var format = new YamlFormatLoader().Load(PngFormatPath);
        var decoded = new BinaryDecoder().Decode(pngData, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("├──");
        output.Should().Contain("└──");
    }
}
