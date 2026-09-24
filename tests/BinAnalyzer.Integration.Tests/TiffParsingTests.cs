using System.Buffers.Binary;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class TiffParsingTests
{
    private static readonly string TiffFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "tiff.bdef.yaml");

    [Fact]
    public void TiffFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(TiffFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void TiffFormat_DecodesMinimalTiff()
    {
        var data = TiffTestDataGenerator.CreateMinimalTiff();
        var format = new YamlFormatLoader().Load(TiffFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("TIFF");
        decoded.Children.Should().HaveCount(2);
        decoded.Children[0].Name.Should().Be("byte_order");
        decoded.Children[1].Name.Should().Be("body");
    }

    [Fact]
    public void TiffFormat_Header_DecodesCorrectly()
    {
        var data = TiffTestDataGenerator.CreateMinimalTiff();
        var format = new YamlFormatLoader().Load(TiffFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var byteOrder = decoded.Children[0].Should().BeOfType<DecodedString>().Subject;
        byteOrder.Value.Should().Be("II");

        var body = decoded.Children[1].Should().BeOfType<DecodedStruct>().Subject;

        var magic = body.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        magic.Value.Should().Be(42);

        var ifdOffset = body.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        ifdOffset.Value.Should().Be(8);
    }

    [Fact]
    public void TiffFormat_Ifd_DecodesCorrectly()
    {
        var data = TiffTestDataGenerator.CreateMinimalTiff();
        var format = new YamlFormatLoader().Load(TiffFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var body = decoded.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var ifd = body.Children[2].Should().BeOfType<DecodedStruct>().Subject;

        var entryCount = ifd.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        entryCount.Value.Should().Be(1);

        var entries = ifd.Children[1].Should().BeOfType<DecodedArray>().Subject;
        entries.Elements.Should().HaveCount(1);

        var entry = entries.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var tag = entry.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        tag.Value.Should().Be(256);
        tag.EnumLabel.Should().Be("ImageWidth");
    }

    [Fact]
    public void TiffFormat_InlineValues_DecodesCorrectly()
    {
        var data = TiffTestDataGenerator.CreateMinimalTiff();
        var format = new YamlFormatLoader().Load(TiffFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var body = decoded.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var ifd = body.Children[2].Should().BeOfType<DecodedStruct>().Subject;
        var entries = ifd.Children[1].Should().BeOfType<DecodedArray>().Subject;
        var entry = entries.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        // entry: tag=256(ImageWidth), field_type=3(SHORT), count=1, value_offset=64
        // is_inline should be true (field_type==3 and count<=2)
        var isInline = entry.Children.First(c => c.Name == "is_inline")
            .Should().BeOfType<DecodedVirtual>().Subject;
        isInline.Value.Should().Be(true);

        // inline_short_value should be 64 (value_offset & 0xFFFF = 64)
        var inlineShort = entry.Children.First(c => c.Name == "inline_short_value")
            .Should().BeOfType<DecodedVirtual>().Subject;
        ((long)inlineShort.Value).Should().Be(64);

        // inline_long_value should NOT be present (field_type==3, not 4)
        entry.Children.Should().NotContain(c => c.Name == "inline_long_value");
    }

    [Fact]
    public void TiffFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = TiffTestDataGenerator.CreateMinimalTiff();
        var format = new YamlFormatLoader().Load(TiffFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("TIFF");
        output.Should().Contain("byte_order");
        output.Should().Contain("body");
        output.Should().Contain("ifd0");
        output.Should().Contain("ImageWidth");
    }

    [Fact]
    public void TiffFormat_BigEndian_DecodesCorrectly()
    {
        var data = TiffTestDataGenerator.CreateBigEndianTiff();
        var format = new YamlFormatLoader().Load(TiffFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("TIFF");

        var byteOrder = decoded.Children[0].Should().BeOfType<DecodedString>().Subject;
        byteOrder.Value.Should().Be("MM");

        var body = decoded.Children[1].Should().BeOfType<DecodedStruct>().Subject;

        var magic = body.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        magic.Value.Should().Be(42);

        var ifdOffset = body.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        ifdOffset.Value.Should().Be(8);
    }

    [Fact]
    public void TiffFormat_BigEndian_IfdEntry_DecodesCorrectly()
    {
        var data = TiffTestDataGenerator.CreateBigEndianTiff();
        var format = new YamlFormatLoader().Load(TiffFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var body = decoded.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var ifd = body.Children[2].Should().BeOfType<DecodedStruct>().Subject;

        var entryCount = ifd.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        entryCount.Value.Should().Be(1);

        var entries = ifd.Children[1].Should().BeOfType<DecodedArray>().Subject;
        entries.Elements.Should().HaveCount(1);

        var entry = entries.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var tag = entry.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        tag.Value.Should().Be(256);
        tag.EnumLabel.Should().Be("ImageWidth");

        // BE の SHORT は値欄の上位 16 ビット（REQ-186 で修正。以前は下位 16 ビットを読んで 0 になっていた）
        var inline = entry.Children.OfType<DecodedVirtual>().Single(c => c.Name == "inline_short_value");
        inline.Value.Should().Be(64L);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TiffFormat_CompressionTag_HasEnumLabel(bool bigEndian)
    {
        // IFD: ImageWidth=64, Compression=5 (LZW)。SHORT は値欄の先頭に詰める
        var entries = new (ushort Tag, ushort Value)[] { (256, 64), (259, 5) };
        var data = new byte[8 + 2 + entries.Length * 12 + 4];
        void U16(int at, ushort v) { if (bigEndian) BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(at), v); else BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(at), v); }
        void U32(int at, uint v) { if (bigEndian) BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(at), v); else BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(at), v); }
        data[0] = data[1] = (byte)(bigEndian ? 'M' : 'I');
        U16(2, 42);
        U32(4, 8);
        U16(8, (ushort)entries.Length);
        for (var i = 0; i < entries.Length; i++)
        {
            var at = 10 + i * 12;
            U16(at, entries[i].Tag);
            U16(at + 2, 3);   // SHORT
            U32(at + 4, 1);   // count
            U16(at + 8, entries[i].Value);
        }

        var format = new YamlFormatLoader().Load(TiffFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var body = decoded.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var ifd = body.Children[2].Should().BeOfType<DecodedStruct>().Subject;
        var array = ifd.Children[1].Should().BeOfType<DecodedArray>().Subject;
        var compression = ((DecodedStruct)array.Elements[1]).Children.OfType<DecodedVirtual>().Single(c => c.Name == "compression");
        compression.Value.Should().Be(5L);
        compression.EnumLabel.Should().Be("LZW");
        ((DecodedStruct)array.Elements[0]).Children.OfType<DecodedVirtual>().Should().NotContain(c => c.Name == "compression");
    }

    [Fact]
    public void TiffFormat_RationalTag_SeeksAndDecodesValue()
    {
        var data = TiffTestDataGenerator.CreateTiffWithRationalTag();
        var format = new YamlFormatLoader().Load(TiffFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var body = decoded.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var ifd = body.Children[2].Should().BeOfType<DecodedStruct>().Subject;
        var entries = ifd.Children[1].Should().BeOfType<DecodedArray>().Subject;
        var entry = entries.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        // tag=282 (XResolution), field_type=5 (RATIONAL)
        var tag = entry.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        tag.Value.Should().Be(282);
        tag.EnumLabel.Should().Be("XResolution");

        var fieldType = entry.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        fieldType.Value.Should().Be(5);

        // rational_value should be present (field_type==5 and count==1)
        var rational = entry.Children.First(c => c.Name == "rational_value")
            .Should().BeOfType<DecodedStruct>().Subject;

        var numerator = rational.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        numerator.Name.Should().Be("numerator");
        numerator.Value.Should().Be(72);

        var denominator = rational.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        denominator.Name.Should().Be("denominator");
        denominator.Value.Should().Be(1);
    }

    [Fact]
    public void TiffFormat_BigEndian_RationalTag_DecodesCorrectly()
    {
        var data = TiffTestDataGenerator.CreateBigEndianTiffWithRational();
        var format = new YamlFormatLoader().Load(TiffFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var body = decoded.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var ifd = body.Children[2].Should().BeOfType<DecodedStruct>().Subject;
        var entries = ifd.Children[1].Should().BeOfType<DecodedArray>().Subject;
        var entry = entries.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        var rational = entry.Children.First(c => c.Name == "rational_value")
            .Should().BeOfType<DecodedStruct>().Subject;

        var numerator = rational.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        numerator.Value.Should().Be(72);

        var denominator = rational.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        denominator.Value.Should().Be(1);
    }
}
