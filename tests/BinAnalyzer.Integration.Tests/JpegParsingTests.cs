using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class JpegParsingTests
{
    private static readonly string JpegFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "jpeg.bdef.yaml");

    [Fact]
    public void JpegFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(JpegFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void JpegFormat_DecodesMinimalJpeg()
    {
        var data = JpegTestDataGenerator.CreateMinimalJpeg();
        var format = new YamlFormatLoader().Load(JpegFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("JPEG");
        decoded.Children.Should().HaveCount(2);
        decoded.Children[0].Name.Should().Be("soi");
        decoded.Children[1].Name.Should().Be("segments");
    }

    [Fact]
    public void JpegFormat_Soi_DecodesCorrectly()
    {
        var data = JpegTestDataGenerator.CreateMinimalJpeg();
        var format = new YamlFormatLoader().Load(JpegFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var soi = decoded.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        soi.ValidationPassed.Should().BeTrue();
    }

    [Fact]
    public void JpegFormat_App0Segment_DecodesCorrectly()
    {
        var data = JpegTestDataGenerator.CreateMinimalJpeg();
        var format = new YamlFormatLoader().Load(JpegFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var segments = decoded.Children[1].Should().BeOfType<DecodedArray>().Subject;
        segments.Elements.Should().HaveCountGreaterThanOrEqualTo(1);

        var app0Segment = segments.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var markerType = app0Segment.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        markerType.Value.Should().Be(0xE0);
        markerType.EnumLabel.Should().Be("APP0");
    }

    [Fact]
    public void JpegFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = JpegTestDataGenerator.CreateMinimalJpeg();
        var format = new YamlFormatLoader().Load(JpegFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("JPEG");
        output.Should().Contain("soi");
        output.Should().Contain("segments");
        output.Should().Contain("APP0");
    }
}
