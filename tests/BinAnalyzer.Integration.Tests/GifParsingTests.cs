using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class GifParsingTests
{
    private static readonly string GifFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "gif.bdef.yaml");

    [Fact]
    public void GifFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(GifFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void GifFormat_DecodesSuccessfully()
    {
        var data = GifTestDataGenerator.CreateMinimalGif();
        var format = new YamlFormatLoader().Load(GifFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("GIF");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(3);
        decoded.Children[0].Name.Should().Be("signature");
        decoded.Children[1].Name.Should().Be("version");
        decoded.Children[2].Name.Should().Be("logical_screen_desc");
    }

    [Fact]
    public void GifFormat_Header_DecodesCorrectly()
    {
        var data = GifTestDataGenerator.CreateMinimalGif();
        var format = new YamlFormatLoader().Load(GifFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var sig = decoded.Children[0].Should().BeOfType<DecodedString>().Subject;
        sig.Value.Should().Be("GIF");

        var version = decoded.Children[1].Should().BeOfType<DecodedString>().Subject;
        version.Value.Should().Be("89a");

        var lsd = decoded.Children[2].Should().BeOfType<DecodedStruct>().Subject;
        var width = lsd.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        width.Value.Should().Be(1);

        // bitfieldエントリ値が正しく解析される
        var packed = lsd.Children[2].Should().BeOfType<DecodedBitfield>().Subject;
        packed.Name.Should().Be("packed");
        packed.Fields.Should().Contain(f => f.Name == "global_color_table_flag" && f.Value == 0);
    }

    [Fact]
    public void GifFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = GifTestDataGenerator.CreateMinimalGif();
        var format = new YamlFormatLoader().Load(GifFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("GIF");
        output.Should().Contain("signature");
        output.Should().Contain("logical_screen_desc");
    }

    [Fact]
    public void GifFormat_ImageBlock_SubBlocks_DecodedAsArray()
    {
        var data = GifTestDataGenerator.CreateGifWithImageBlock();
        var format = new YamlFormatLoader().Load(GifFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        // blocks 配列を取得
        var blocks = decoded.Children[4].Should().BeOfType<DecodedArray>().Subject;
        blocks.Elements.Should().HaveCountGreaterThanOrEqualTo(1);

        // 最初のブロックはイメージブロック (0x2C)
        var imageBlockOuter = blocks.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var body = imageBlockOuter.Children[1].Should().BeOfType<DecodedStruct>().Subject;

        // image_block 内の sub_blocks を検証
        var subBlocks = body.Children.FirstOrDefault(c => c.Name == "sub_blocks");
        subBlocks.Should().NotBeNull();
        var subBlockArray = subBlocks.Should().BeOfType<DecodedArray>().Subject;
        subBlockArray.Elements.Should().HaveCount(2);

        // 1st sub-block: 5 bytes
        var sb1 = subBlockArray.Elements[0].Should().BeOfType<DecodedBytes>().Subject;
        sb1.Size.Should().Be(5);
        sb1.RawBytes.ToArray().Should().Equal(0x04, 0x01, 0x00, 0x00, 0x02);

        // 2nd sub-block: 3 bytes
        var sb2 = subBlockArray.Elements[1].Should().BeOfType<DecodedBytes>().Subject;
        sb2.Size.Should().Be(3);
        sb2.RawBytes.ToArray().Should().Equal(0x05, 0x00, 0x00);
    }
}
