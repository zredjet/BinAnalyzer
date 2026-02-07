using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class AviParsingTests
{
    private static readonly string AviFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "avi.bdef.yaml");

    [Fact]
    public void AviFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(AviFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AviFormat_DecodesWithRecovery()
    {
        var data = AviTestDataGenerator.CreateMinimalAvi();
        var format = new YamlFormatLoader().Load(AviFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        decoded.Name.Should().Be("AVI");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(3);
        decoded.Children[0].Name.Should().Be("magic");
        decoded.Children[1].Name.Should().Be("file_size");
        decoded.Children[2].Name.Should().Be("form_type");
    }

    [Fact]
    public void AviFormat_RiffHeader_DecodesCorrectly()
    {
        var data = AviTestDataGenerator.CreateMinimalAvi();
        var format = new YamlFormatLoader().Load(AviFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var magic = decoded.Children[0].Should().BeOfType<DecodedString>().Subject;
        magic.Value.Should().Be("RIFF");

        var formType = decoded.Children[2].Should().BeOfType<DecodedString>().Subject;
        formType.Value.Should().Be("AVI ");
    }

    [Fact]
    public void AviFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = AviTestDataGenerator.CreateMinimalAvi();
        var format = new YamlFormatLoader().Load(AviFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var output = new TreeOutputFormatter().Format(result.Root);

        output.Should().Contain("AVI");
        output.Should().Contain("magic");
        output.Should().Contain("form_type");
    }
}
