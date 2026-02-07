using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class SevenZipParsingTests
{
    private static readonly string SevenZipFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "7z.bdef.yaml");

    [Fact]
    public void SevenZipFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(SevenZipFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void SevenZipFormat_DecodesMinimal7z()
    {
        var data = SevenZipTestDataGenerator.CreateMinimal7z();
        var format = new YamlFormatLoader().Load(SevenZipFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("7z");
        decoded.Children.Should().HaveCount(2);
        decoded.Children[0].Name.Should().Be("signature_header");
        decoded.Children[1].Name.Should().Be("next_header");
    }

    [Fact]
    public void SevenZipFormat_SignatureHeader_DecodesCorrectly()
    {
        var data = SevenZipTestDataGenerator.CreateMinimal7z();
        var format = new YamlFormatLoader().Load(SevenZipFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var sigHeader = decoded.Children[0].Should().BeOfType<DecodedStruct>().Subject;

        var signature = sigHeader.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        signature.Name.Should().Be("signature");
        signature.ValidationPassed.Should().BeTrue();

        var majorVersion = sigHeader.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        majorVersion.Name.Should().Be("major_version");
        majorVersion.Value.Should().Be(0);

        var minorVersion = sigHeader.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        minorVersion.Name.Should().Be("minor_version");
        minorVersion.Value.Should().Be(4);
    }

    [Fact]
    public void SevenZipFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = SevenZipTestDataGenerator.CreateMinimal7z();
        var format = new YamlFormatLoader().Load(SevenZipFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("7z");
        output.Should().Contain("signature_header");
        output.Should().Contain("signature");
        output.Should().Contain("next_header");
    }
}
