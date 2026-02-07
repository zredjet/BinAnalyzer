using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class PeParsingTests
{
    private static readonly string PeFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "pe.bdef.yaml");

    [Fact]
    public void PeFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(PeFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void PeFormat_DecodesWithRecovery()
    {
        var data = PeTestDataGenerator.CreateMinimalPe();
        var format = new YamlFormatLoader().Load(PeFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        decoded.Name.Should().Be("PE");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(3);
        decoded.Children[0].Name.Should().Be("dos_header");
        decoded.Children[1].Name.Should().Be("pe_signature");
        decoded.Children[2].Name.Should().Be("coff_header");
    }

    [Fact]
    public void PeFormat_DosHeader_DecodesCorrectly()
    {
        var data = PeTestDataGenerator.CreateMinimalPe();
        var format = new YamlFormatLoader().Load(PeFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var dosHeader = decoded.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        var eMagic = dosHeader.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        eMagic.ValidationPassed.Should().BeTrue();

        var eLfanew = dosHeader.Children.Last().Should().BeOfType<DecodedInteger>().Subject;
        eLfanew.Name.Should().Be("e_lfanew");
        eLfanew.Value.Should().Be(64);
    }

    [Fact]
    public void PeFormat_CoffHeader_DecodesCorrectly()
    {
        var data = PeTestDataGenerator.CreateMinimalPe();
        var format = new YamlFormatLoader().Load(PeFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var peSignature = decoded.Children[1].Should().BeOfType<DecodedBytes>().Subject;
        peSignature.ValidationPassed.Should().BeTrue();

        var coffHeader = decoded.Children[2].Should().BeOfType<DecodedStruct>().Subject;
        var machine = coffHeader.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        machine.EnumLabel.Should().Be("IMAGE_FILE_MACHINE_AMD64");

        var numSections = coffHeader.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        numSections.Value.Should().Be(1);
    }

    [Fact]
    public void PeFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = PeTestDataGenerator.CreateMinimalPe();
        var format = new YamlFormatLoader().Load(PeFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var output = new TreeOutputFormatter().Format(result.Root);

        output.Should().Contain("PE");
        output.Should().Contain("dos_header");
        output.Should().Contain("pe_signature");
        output.Should().Contain("coff_header");
        output.Should().Contain("IMAGE_FILE_MACHINE_AMD64");
    }
}
