using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class WebpParsingTests
{
    private static readonly string WebpFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "webp.bdef.yaml");

    [Fact]
    public void WebpFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(WebpFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void WebpFormat_DecodesSuccessfully()
    {
        var data = WebpTestDataGenerator.CreateMinimalWebp();
        var format = new YamlFormatLoader().Load(WebpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("WebP");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(3);
        decoded.Children[0].Name.Should().Be("riff_magic");
        decoded.Children[1].Name.Should().Be("file_size");
        decoded.Children[2].Name.Should().Be("webp_magic");
    }

    [Fact]
    public void WebpFormat_Magic_DecodesCorrectly()
    {
        var data = WebpTestDataGenerator.CreateMinimalWebp();
        var format = new YamlFormatLoader().Load(WebpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var riffMagic = decoded.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        riffMagic.ValidationPassed.Should().BeTrue();

        var webpMagic = decoded.Children[2].Should().BeOfType<DecodedBytes>().Subject;
        webpMagic.ValidationPassed.Should().BeTrue();
    }

    [Fact]
    public void WebpFormat_VP8FrameTag_BitfieldDecodesCorrectly()
    {
        var data = WebpTestDataGenerator.CreateMinimalWebp();
        var format = new YamlFormatLoader().Load(WebpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        // Navigate to chunks[0].data (VP8 chunk) → frame_tag bitfield
        var chunks = (DecodedArray)decoded.Children[3]; // chunks (repeat → array)
        var chunk = (DecodedStruct)chunks.Elements[0]; // first chunk struct (VP8)
        var vp8Data = (DecodedStruct)chunk.Children[2]; // data (switch → vp8_data)
        var frameTag = vp8Data.Children[0].Should().BeOfType<DecodedBitfield>().Subject;

        frameTag.Name.Should().Be("frame_tag");
        frameTag.Fields.Should().Contain(f => f.Name == "frame_type" && f.Value == 0); // keyframe
        frameTag.Fields.Should().Contain(f => f.Name == "show_frame" && f.Value == 1);
    }

    [Fact]
    public void WebpFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = WebpTestDataGenerator.CreateMinimalWebp();
        var format = new YamlFormatLoader().Load(WebpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("WebP");
        output.Should().Contain("riff_magic");
        output.Should().Contain("webp_magic");
    }
}
