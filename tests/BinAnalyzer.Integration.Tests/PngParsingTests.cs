using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class PngParsingTests
{
    private static readonly string PngFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "png.bdef.yaml");

    private readonly YamlFormatLoader _loader = new();
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void Parse_MinimalPng_ProducesCorrectTree()
    {
        var pngData = PngTestDataGenerator.CreateMinimalPng();
        var format = _loader.Load(PngFormatPath);

        var result = _decoder.Decode(pngData, format);

        result.Name.Should().Be("PNG");
        result.StructType.Should().Be("png");
        result.Children.Should().HaveCount(2);

        // Signature
        var sig = result.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        sig.Name.Should().Be("signature");
        sig.ValidationPassed.Should().BeTrue();

        // Chunks array
        var chunks = result.Children[1].Should().BeOfType<DecodedArray>().Subject;
        chunks.Name.Should().Be("chunks");
        chunks.Elements.Should().HaveCount(2); // IHDR + IEND
    }

    [Fact]
    public void Parse_MinimalPng_IhdrChunkDecodedCorrectly()
    {
        var pngData = PngTestDataGenerator.CreateMinimalPng();
        var format = _loader.Load(PngFormatPath);

        var result = _decoder.Decode(pngData, format);

        var chunks = (DecodedArray)result.Children[1];
        var ihdrChunk = chunks.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        // chunk has: length, type, data, crc
        ihdrChunk.Children.Should().HaveCount(4);

        var length = ihdrChunk.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        length.Name.Should().Be("length");
        length.Value.Should().Be(13);

        var type = ihdrChunk.Children[1].Should().BeOfType<DecodedString>().Subject;
        type.Name.Should().Be("type");
        type.Value.Should().Be("IHDR");

        // data is the decoded IHDR struct (via switch)
        var data = ihdrChunk.Children[2].Should().BeOfType<DecodedStruct>().Subject;
        data.StructType.Should().Be("ihdr");

        var width = data.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        width.Name.Should().Be("width");
        width.Value.Should().Be(1);

        var height = data.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        height.Name.Should().Be("height");
        height.Value.Should().Be(1);

        var colorType = data.Children[3].Should().BeOfType<DecodedInteger>().Subject;
        colorType.Name.Should().Be("color_type");
        colorType.Value.Should().Be(2);
        colorType.EnumLabel.Should().Be("truecolor");
    }

    [Fact]
    public void Parse_PngWithSrgb_DecodesAllChunks()
    {
        var pngData = PngTestDataGenerator.CreatePngWithSrgb();
        var format = _loader.Load(PngFormatPath);

        var result = _decoder.Decode(pngData, format);

        var chunks = (DecodedArray)result.Children[1];
        chunks.Elements.Should().HaveCount(3); // IHDR + sRGB + IEND

        // sRGB chunk
        var srgbChunk = chunks.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        var srgbType = srgbChunk.Children[1].Should().BeOfType<DecodedString>().Subject;
        srgbType.Value.Should().Be("sRGB");

        var srgbData = srgbChunk.Children[2].Should().BeOfType<DecodedStruct>().Subject;
        srgbData.StructType.Should().Be("srgb_chunk");
        var intent = srgbData.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        intent.EnumLabel.Should().Be("perceptual");
    }

    [Fact]
    public void Parse_MinimalPng_ChunkTypeHasFlags()
    {
        var pngData = PngTestDataGenerator.CreateMinimalPng();
        var format = _loader.Load(PngFormatPath);

        var result = _decoder.Decode(pngData, format);

        var chunks = (DecodedArray)result.Children[1];
        var ihdrChunk = (DecodedStruct)chunks.Elements[0];
        var type = ihdrChunk.Children[1].Should().BeOfType<DecodedString>().Subject;
        type.Flags.Should().NotBeNull();
        type.Flags!.Count.Should().Be(4);

        // IHDR: all uppercase → ancillary=no, private=no, reserved=no, safe_to_copy=no
        var ancillary = type.Flags.First(f => f.Name == "ancillary");
        ancillary.IsSet.Should().BeFalse();
        ancillary.Meaning.Should().Be("no");
    }

    // --- REQ-188: PNG 第 3 版のチャンク・APNG・カラータイプで形が変わるチャンク ---

    private DecodedStruct ChunkData(DecodedStruct root, string type, int occurrence = 0)
    {
        var chunk = ((DecodedArray)root.Children[1]).Elements.Cast<DecodedStruct>()
            .Where(c => ((DecodedString)c.Children[1]).Value == type)
            .ElementAt(occurrence);
        chunk.Children.OfType<DecodedInteger>().Single(n => n.Name == "crc").ChecksumValid.Should().BeTrue(type);
        return (DecodedStruct)chunk.Children[2];
    }

    private static long Int(DecodedStruct s, string name) => ((DecodedInteger)s.Children.Single(c => c.Name == name)).Value;

    private DecodedStruct DecodeThirdEdition() =>
        _decoder.Decode(PngTestDataGenerator.CreateThirdEditionApng(), _loader.Load(PngFormatPath));

    [Fact]
    public void ThirdEdition_ColourChunks()
    {
        var root = DecodeThirdEdition();

        var cicp = ChunkData(root, "cICP");
        ((DecodedInteger)cicp.Children[0]).EnumLabel.Should().Be("BT2020");
        ((DecodedInteger)cicp.Children[1]).EnumLabel.Should().Be("PQ");
        cicp.Children[2].ValidationPassed.Should().NotBe(false);
        Int(ChunkData(root, "mDCV"), "max_luminance").Should().Be(10_000_000);
        Int(ChunkData(root, "cLLI"), "max_frame_average_light_level").Should().Be(4_000_000);

        var iccp = ChunkData(root, "iCCP");
        ((DecodedString)iccp.Children[0]).Value.Should().Be("icc");
        iccp.Children.OfType<DecodedCompressed>().Single().DecompressedSize.Should().Be("not a real ICC profile".Length);
    }

    [Fact]
    public void Apng_AnimationAndFrameControl()
    {
        var root = DecodeThirdEdition();

        Int(ChunkData(root, "acTL"), "num_frames").Should().Be(2);
        var second = ChunkData(root, "fcTL", occurrence: 1);
        Int(second, "sequence_number").Should().Be(1);
        ((DecodedInteger)second.Children.Single(c => c.Name == "dispose_op")).EnumLabel.Should().Be("background");
        ((DecodedInteger)second.Children.Single(c => c.Name == "blend_op")).EnumLabel.Should().Be("over");
        Int(ChunkData(root, "fdAT"), "sequence_number").Should().Be(2);
    }

    [Fact]
    public void ColourTypeDependentChunks_AreDecodedByIhdrColourType()
    {
        var root = DecodeThirdEdition();   // カラータイプ 2（RGB）

        var sbit = (DecodedStruct)ChunkData(root, "sBIT").Children[0];
        sbit.StructType.Should().Be("sbit_rgb");
        Int(sbit, "green").Should().Be(6);
        var bkgd = (DecodedStruct)ChunkData(root, "bKGD").Children[0];
        bkgd.StructType.Should().Be("bkgd_rgb");
        Int(bkgd, "blue").Should().Be(3);
        ((DecodedArray)ChunkData(root, "hIST").Children[0]).Elements.Should().HaveCount(2);
        var splt = ChunkData(root, "sPLT");
        ((DecodedString)splt.Children[0]).Value.Should().Be("suggested");
        ((DecodedStruct)splt.Children[2]).StructType.Should().Be("splt_entries8");
        ((DecodedString)ChunkData(root, "eXIf").Children[0]).Value.Should().Be("MM");
        Int(ChunkData(root, "tIME"), "year").Should().Be(2026);
    }
}
