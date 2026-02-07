using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class Mp3ParsingTests
{
    private static readonly string Mp3FormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "mp3.bdef.yaml");

    [Fact]
    public void Mp3Format_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(Mp3FormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Mp3Format_DecodesWithRecovery()
    {
        var data = Mp3TestDataGenerator.CreateMinimalMp3();
        var format = new YamlFormatLoader().Load(Mp3FormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        decoded.Name.Should().Be("MP3");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(1);
        decoded.Children[0].Name.Should().Be("id3v2_header");
    }

    [Fact]
    public void Mp3Format_Id3Header_DecodesCorrectly()
    {
        var data = Mp3TestDataGenerator.CreateMinimalMp3();
        var format = new YamlFormatLoader().Load(Mp3FormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var id3Header = decoded.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        var magic = id3Header.Children[0].Should().BeOfType<DecodedString>().Subject;
        magic.Value.Should().Be("ID3");

        var version = id3Header.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        version.Value.Should().Be(3);
    }

    [Fact]
    public void Mp3Format_TreeOutput_ContainsExpectedElements()
    {
        var data = Mp3TestDataGenerator.CreateMinimalMp3();
        var format = new YamlFormatLoader().Load(Mp3FormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var output = new TreeOutputFormatter().Format(result.Root);

        output.Should().Contain("MP3");
        output.Should().Contain("id3v2_header");
    }
}
