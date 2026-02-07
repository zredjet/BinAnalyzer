using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class Lz4ParsingTests
{
    private static readonly string Lz4FormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "lz4.bdef.yaml");

    [Fact]
    public void Lz4Format_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(Lz4FormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Lz4Format_DecodesWithRecovery()
    {
        var data = Lz4TestDataGenerator.CreateMinimalLz4();
        var format = new YamlFormatLoader().Load(Lz4FormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        decoded.Name.Should().Be("LZ4");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(3);
        decoded.Children[0].Name.Should().Be("magic");
        decoded.Children[1].Name.Should().Be("flg");
        decoded.Children[2].Name.Should().Be("bd");
    }

    [Fact]
    public void Lz4Format_Magic_DecodesCorrectly()
    {
        var data = Lz4TestDataGenerator.CreateMinimalLz4();
        var format = new YamlFormatLoader().Load(Lz4FormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var magic = decoded.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        magic.Value.Should().Be(0x184D2204);
    }

    [Fact]
    public void Lz4Format_Bitfields_DecodeCorrectly()
    {
        var data = Lz4TestDataGenerator.CreateMinimalLz4();
        var format = new YamlFormatLoader().Load(Lz4FormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var flg = decoded.Children[1].Should().BeOfType<DecodedBitfield>().Subject;
        flg.Name.Should().Be("flg");

        var bd = decoded.Children[2].Should().BeOfType<DecodedBitfield>().Subject;
        bd.Name.Should().Be("bd");
    }

    [Fact]
    public void Lz4Format_TreeOutput_ContainsExpectedElements()
    {
        var data = Lz4TestDataGenerator.CreateMinimalLz4();
        var format = new YamlFormatLoader().Load(Lz4FormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var output = new TreeOutputFormatter().Format(result.Root);

        output.Should().Contain("LZ4");
        output.Should().Contain("magic");
        output.Should().Contain("flg");
        output.Should().Contain("bd");
    }
}
