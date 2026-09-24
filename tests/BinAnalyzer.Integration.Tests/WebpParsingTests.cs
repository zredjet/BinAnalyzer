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

    // --- REQ-188: RFC 9649 との照合（VP8X のビット位置、VP8L のヘッダ、ANMF のサブチャンク、XMP） ---

    private static IEnumerable<DecodedStruct> Chunks(DecodedStruct root) =>
        ((DecodedArray)root.Children.Single(c => c.Name == "chunks")).Elements.Cast<DecodedStruct>();

    private static DecodedStruct Data(DecodedStruct chunk) => (DecodedStruct)chunk.Children.Single(c => c.Name == "data");

    private static string Id(DecodedStruct chunk) => ((DecodedString)chunk.Children[0]).Value;

    private static long Bit(DecodedStruct data, string field, string bit) =>
        ((DecodedBitfield)data.Children.Single(c => c.Name == field)).Fields.Single(f => f.Name == bit).Value;

    [Fact]
    public void AnimatedWebp_Vp8xFlagsFollowRfc9649()
    {
        var root = new BinaryDecoder().Decode(WebpTestDataGenerator.CreateAnimatedWebp(), new YamlFormatLoader().Load(WebpFormatPath));
        var vp8x = Data(Chunks(root).Single(c => Id(c) == "VP8X"));

        // 0x16 = アルファ（ビット 4）| XMP（ビット 2）| アニメーション（ビット 1）
        Bit(vp8x, "flags", "alpha_channel").Should().Be(1);
        Bit(vp8x, "flags", "xmp_metadata").Should().Be(1);
        Bit(vp8x, "flags", "animation").Should().Be(1);
        Bit(vp8x, "flags", "icc_profile").Should().Be(0);
        Bit(vp8x, "flags", "exif_metadata").Should().Be(0);
        ((DecodedVirtual)vp8x.Children.Single(c => c.Name == "canvas_width")).Value.Should().Be(4L);
    }

    [Fact]
    public void AnimatedWebp_FramesContainVp8lSubChunks()
    {
        var root = new BinaryDecoder().Decode(WebpTestDataGenerator.CreateAnimatedWebp(), new YamlFormatLoader().Load(WebpFormatPath));
        var frames = Chunks(root).Where(c => Id(c) == "ANMF").Select(Data).ToList();

        frames.Should().HaveCount(2);
        ((DecodedVirtual)frames[1].Children.Single(c => c.Name == "duration")).Value.Should().Be(80L);
        Bit(frames[0], "flags", "blending_method").Should().Be(1);
        var sub = ((DecodedArray)frames[0].Children.Single(c => c.Name == "frame_chunks")).Elements.Cast<DecodedStruct>().Single();
        Id(sub).Should().Be("VP8L");
        var vp8l = Data(sub);
        ((DecodedVirtual)vp8l.Children.Single(c => c.Name == "width")).Value.Should().Be(2L);
        ((DecodedVirtual)vp8l.Children.Single(c => c.Name == "height")).Value.Should().Be(3L);
        Bit(vp8l, "header", "alpha_is_used").Should().Be(1);
    }

    [Fact]
    public void AnimatedWebp_XmpChunkIsText()
    {
        var root = new BinaryDecoder().Decode(WebpTestDataGenerator.CreateAnimatedWebp(), new YamlFormatLoader().Load(WebpFormatPath));

        ((DecodedString)Data(Chunks(root).Single(c => Id(c) == "XMP ")).Children[0]).Value.Should().Be("<x:xmpmeta/>");
    }
}
