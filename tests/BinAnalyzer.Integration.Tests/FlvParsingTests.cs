using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class FlvParsingTests
{
    private static readonly string FlvFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "flv.bdef.yaml");

    [Fact]
    public void FlvFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(FlvFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void FlvFormat_DecodesWithRecovery()
    {
        var data = FlvTestDataGenerator.CreateMinimalFlv();
        var format = new YamlFormatLoader().Load(FlvFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        decoded.Name.Should().Be("FLV");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(5);
        decoded.Children[0].Name.Should().Be("signature");
        decoded.Children[1].Name.Should().Be("version");
        decoded.Children[2].Name.Should().Be("flags");
        decoded.Children[3].Name.Should().Be("data_offset");
        decoded.Children[4].Name.Should().Be("prev_tag_size_0");
    }

    [Fact]
    public void FlvFormat_Signature_DecodesCorrectly()
    {
        var data = FlvTestDataGenerator.CreateMinimalFlv();
        var format = new YamlFormatLoader().Load(FlvFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var sig = decoded.Children[0].Should().BeOfType<DecodedString>().Subject;
        sig.Value.Should().Be("FLV");
    }

    [Fact]
    public void FlvFormat_Header_DecodesCorrectly()
    {
        var data = FlvTestDataGenerator.CreateMinimalFlv();
        var format = new YamlFormatLoader().Load(FlvFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var version = decoded.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        version.Value.Should().Be(1);

        var dataOffset = decoded.Children[3].Should().BeOfType<DecodedInteger>().Subject;
        dataOffset.Value.Should().Be(9);
    }

    [Fact]
    public void FlvFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = FlvTestDataGenerator.CreateMinimalFlv();
        var format = new YamlFormatLoader().Load(FlvFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var output = new TreeOutputFormatter().Format(result.Root);

        output.Should().Contain("FLV");
        output.Should().Contain("signature");
        output.Should().Contain("data_offset");
    }
}
