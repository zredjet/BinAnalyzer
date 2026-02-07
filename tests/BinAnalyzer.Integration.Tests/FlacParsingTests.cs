using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class FlacParsingTests
{
    private static readonly string FlacFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "flac.bdef.yaml");

    [Fact]
    public void FlacFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(FlacFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void FlacFormat_DecodesWithRecovery()
    {
        var data = FlacTestDataGenerator.CreateMinimalFlac();
        var format = new YamlFormatLoader().Load(FlacFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        decoded.Name.Should().Be("FLAC");
        decoded.Children.Should().HaveCount(2);
        decoded.Children[0].Name.Should().Be("magic");
        decoded.Children[1].Name.Should().Be("metadata_blocks");
    }

    [Fact]
    public void FlacFormat_Magic_DecodesCorrectly()
    {
        var data = FlacTestDataGenerator.CreateMinimalFlac();
        var format = new YamlFormatLoader().Load(FlacFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var magic = decoded.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        magic.ValidationPassed.Should().BeTrue();
    }

    [Fact]
    public void FlacFormat_MetadataBlock_DecodesWithRecovery()
    {
        var data = FlacTestDataGenerator.CreateMinimalFlac();
        var format = new YamlFormatLoader().Load(FlacFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        decoded.Children.Should().HaveCount(2);
        decoded.Children[1].Name.Should().Be("metadata_blocks");
    }

    [Fact]
    public void FlacFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = FlacTestDataGenerator.CreateMinimalFlac();
        var format = new YamlFormatLoader().Load(FlacFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var output = new TreeOutputFormatter().Format(result.Root);

        output.Should().Contain("FLAC");
        output.Should().Contain("magic");
        output.Should().Contain("metadata_blocks");
    }
}
