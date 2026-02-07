using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class IcoParsingTests
{
    private static readonly string IcoFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "ico.bdef.yaml");

    [Fact]
    public void IcoFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(IcoFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void IcoFormat_DecodesWithRecovery()
    {
        var data = IcoTestDataGenerator.CreateMinimalIco();
        var format = new YamlFormatLoader().Load(IcoFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        decoded.Name.Should().Be("ICO");
        decoded.Children.Should().HaveCount(4);
        decoded.Children[0].Name.Should().Be("reserved");
        decoded.Children[1].Name.Should().Be("type");
        decoded.Children[2].Name.Should().Be("count");
        decoded.Children[3].Name.Should().Be("entries");
    }

    [Fact]
    public void IcoFormat_Header_DecodesCorrectly()
    {
        var data = IcoTestDataGenerator.CreateMinimalIco();
        var format = new YamlFormatLoader().Load(IcoFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var type = decoded.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        type.Value.Should().Be(1);
        type.EnumLabel.Should().Be("ICO");

        var count = decoded.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        count.Value.Should().Be(1);
    }

    [Fact]
    public void IcoFormat_Entry_DecodesCorrectly()
    {
        var data = IcoTestDataGenerator.CreateMinimalIco();
        var format = new YamlFormatLoader().Load(IcoFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var entries = decoded.Children[3].Should().BeOfType<DecodedArray>().Subject;
        entries.Elements.Should().HaveCount(1);

        var entry = entries.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var width = entry.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        width.Name.Should().Be("width");
        width.Value.Should().Be(1);

        var bpp = entry.Children[5].Should().BeOfType<DecodedInteger>().Subject;
        bpp.Name.Should().Be("bpp");
        bpp.Value.Should().Be(32);
    }

    [Fact]
    public void IcoFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = IcoTestDataGenerator.CreateMinimalIco();
        var format = new YamlFormatLoader().Load(IcoFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var output = new TreeOutputFormatter().Format(result.Root);

        output.Should().Contain("ICO");
        output.Should().Contain("entries");
        output.Should().Contain("width");
        output.Should().Contain("image_offset");
    }
}
