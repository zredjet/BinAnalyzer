using BinAnalyzer.Core.Decoded;
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
    public void Mp3Format_DecodesSuccessfully()
    {
        var data = Mp3TestDataGenerator.CreateMinimalMp3();
        var format = new YamlFormatLoader().Load(Mp3FormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("MP3");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(1);
        decoded.Children[0].Name.Should().Be("id3v2_header");
    }

    [Fact]
    public void Mp3Format_Id3Header_DecodesCorrectly()
    {
        var data = Mp3TestDataGenerator.CreateMinimalMp3();
        var format = new YamlFormatLoader().Load(Mp3FormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var id3Header = decoded.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        var magic = id3Header.Children[0].Should().BeOfType<DecodedString>().Subject;
        magic.Value.Should().Be("ID3");

        var version = id3Header.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        version.Value.Should().Be(3);

        // virtual field tag_size が解析される
        id3Header.Children.Should().Contain(c => c.Name == "tag_size");
    }

    [Fact]
    public void Mp3Format_TreeOutput_ContainsExpectedElements()
    {
        var data = Mp3TestDataGenerator.CreateMinimalMp3();
        var format = new YamlFormatLoader().Load(Mp3FormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("MP3");
        output.Should().Contain("id3v2_header");
    }
}
