using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class BmpParsingTests
{
    private static readonly string BmpFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "bmp.bdef.yaml");

    [Fact]
    public void BmpFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void BmpFormat_DecodesMinimalBmp()
    {
        var bmpData = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(bmpData, format);

        decoded.Name.Should().Be("BMP");
        decoded.Children.Should().HaveCount(4);
        decoded.Children[0].Name.Should().Be("file_header");
        decoded.Children[1].Name.Should().Be("header_size");
        decoded.Children[2].Name.Should().Be("dib_header");
        decoded.Children[3].Name.Should().Be("pixel_data");
    }

    [Fact]
    public void BmpFormat_FileHeader_DecodesCorrectly()
    {
        var bmpData = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(bmpData, format);

        var fileHeader = decoded.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        var signature = fileHeader.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        signature.ValidationPassed.Should().BeTrue();

        var fileSize = fileHeader.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        fileSize.Value.Should().Be(58);

        var pixelOffset = fileHeader.Children[4].Should().BeOfType<DecodedInteger>().Subject;
        pixelOffset.Value.Should().Be(54);
    }

    [Fact]
    public void BmpFormat_DibHeader_DecodesCorrectly()
    {
        var bmpData = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(bmpData, format);

        var headerSize = decoded.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        headerSize.Value.Should().Be(40);

        var dibHeader = decoded.Children[2].Should().BeOfType<DecodedStruct>().Subject;

        var width = dibHeader.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        width.Value.Should().Be(1);

        var height = dibHeader.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        height.Value.Should().Be(1);

        var bitsPerPixel = dibHeader.Children[3].Should().BeOfType<DecodedInteger>().Subject;
        bitsPerPixel.Value.Should().Be(24);

        var compression = dibHeader.Children[4].Should().BeOfType<DecodedInteger>().Subject;
        compression.Value.Should().Be(0);
        compression.EnumLabel.Should().Be("BI_RGB");
    }

    [Fact]
    public void BmpFormat_TreeOutput_ContainsExpectedElements()
    {
        var bmpData = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(bmpData, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("BMP");
        output.Should().Contain("file_header");
        output.Should().Contain("signature");
        output.Should().Contain("✓");
        output.Should().Contain("dib_header");
        output.Should().Contain("width: 1");
        output.Should().Contain("height: 1");
        output.Should().Contain("BI_RGB");
        output.Should().Contain("pixel_data");
    }

    // --- REQ-188: colors_used = 0 の既定パレット、BI_BITFIELDS のマスク、pixel_offset ---

    [Fact]
    public void ImplicitPalette_HasTwoToTheBppEntries()
    {
        var data = BmpTestDataGenerator.CreateMonochromeWithImplicitPalette();
        var decoded = new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(BmpFormatPath));

        ((DecodedArray)decoded.Children.Single(c => c.Name == "color_table")).Elements.Should().HaveCount(2);
        var pixels = (DecodedBytes)decoded.Children.Single(c => c.Name == "pixel_data");
        pixels.Offset.Should().Be(14 + 40 + 8, "pixel_offset の位置から");
        pixels.Size.Should().Be(4);
    }

    [Fact]
    public void Bitfields_MasksFollowTheInfoHeader()
    {
        var data = BmpTestDataGenerator.CreateRgb565Bitfields();
        var decoded = new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(BmpFormatPath));

        var masks = (DecodedStruct)decoded.Children.Single(c => c.Name == "color_masks");
        ((DecodedInteger)masks.Children[0]).Value.Should().Be(0xF800);
        ((DecodedInteger)masks.Children[1]).Value.Should().Be(0x07E0);
        ((DecodedInteger)masks.Children[2]).Value.Should().Be(0x001F);
        masks.Children.Should().NotContain(c => c.Name == "alpha_mask", "BI_BITFIELDS はアルファのマスクを持たない");
        decoded.Children.Should().NotContain(c => c.Name == "color_table", "16 ビットはパレットを持たない");
        ((DecodedBytes)decoded.Children.Single(c => c.Name == "pixel_data")).Size.Should().Be(4);
    }
}
