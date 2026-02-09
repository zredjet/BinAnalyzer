using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class LengthPrefixedTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void SingleBlock_DecodesOneElement()
    {
        var format = CreateFormat(new FieldDefinition
        {
            Name = "blocks",
            Type = FieldType.Bytes,
            Repeat = new RepeatMode.LengthPrefixed(1),
        });
        // [03][AA BB CC][00]
        var data = new byte[] { 0x03, 0xAA, 0xBB, 0xCC, 0x00 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(1);
        var block = array.Elements[0].Should().BeOfType<DecodedBytes>().Subject;
        block.RawBytes.ToArray().Should().Equal(0xAA, 0xBB, 0xCC);
        block.Size.Should().Be(3);
    }

    [Fact]
    public void MultipleBlocks_DecodesAllElements()
    {
        var format = CreateFormat(new FieldDefinition
        {
            Name = "blocks",
            Type = FieldType.Bytes,
            Repeat = new RepeatMode.LengthPrefixed(1),
        });
        // [02][AA BB][03][CC DD EE][00]
        var data = new byte[] { 0x02, 0xAA, 0xBB, 0x03, 0xCC, 0xDD, 0xEE, 0x00 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);

        var b1 = array.Elements[0].Should().BeOfType<DecodedBytes>().Subject;
        b1.RawBytes.ToArray().Should().Equal(0xAA, 0xBB);

        var b2 = array.Elements[1].Should().BeOfType<DecodedBytes>().Subject;
        b2.RawBytes.ToArray().Should().Equal(0xCC, 0xDD, 0xEE);
    }

    [Fact]
    public void ImmediateTerminator_ReturnsEmptyArray()
    {
        var format = CreateFormat(new FieldDefinition
        {
            Name = "blocks",
            Type = FieldType.Bytes,
            Repeat = new RepeatMode.LengthPrefixed(1),
        });
        // [00] — immediate terminator
        var data = new byte[] { 0x00 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().BeEmpty();
    }

    [Fact]
    public void TerminatorNotIncludedInArray()
    {
        var format = CreateFormat(new FieldDefinition
        {
            Name = "blocks",
            Type = FieldType.Bytes,
            Repeat = new RepeatMode.LengthPrefixed(1),
        });
        // [01][FF][00]
        var data = new byte[] { 0x01, 0xFF, 0x00 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(1);
        // Terminator (0x00) should not be in the array — all elements have non-zero size
        array.Elements.Cast<DecodedBytes>().Should().OnlyContain(b => b.Size > 0);
    }

    [Fact]
    public void PrefixSize2_LittleEndian_DecodesCorrectly()
    {
        var format = CreateFormat(Endianness.Little, new FieldDefinition
        {
            Name = "blocks",
            Type = FieldType.Bytes,
            Repeat = new RepeatMode.LengthPrefixed(2),
        });
        // prefix=0x0003 (LE: 03 00), data=AA BB CC, terminator=00 00
        var data = new byte[] { 0x03, 0x00, 0xAA, 0xBB, 0xCC, 0x00, 0x00 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(1);
        var block = array.Elements[0].Should().BeOfType<DecodedBytes>().Subject;
        block.RawBytes.ToArray().Should().Equal(0xAA, 0xBB, 0xCC);
    }

    [Fact]
    public void PrefixSize4_BigEndian_DecodesCorrectly()
    {
        var format = CreateFormat(Endianness.Big, new FieldDefinition
        {
            Name = "blocks",
            Type = FieldType.Bytes,
            Repeat = new RepeatMode.LengthPrefixed(4),
        });
        // prefix=0x00000002 (BE), data=AA BB, terminator=00 00 00 00
        var data = new byte[] { 0x00, 0x00, 0x00, 0x02, 0xAA, 0xBB, 0x00, 0x00, 0x00, 0x00 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(1);
        var block = array.Elements[0].Should().BeOfType<DecodedBytes>().Subject;
        block.RawBytes.ToArray().Should().Equal(0xAA, 0xBB);
    }

    [Fact]
    public void EofWithoutTerminator_StopsSafely()
    {
        var format = CreateFormat(new FieldDefinition
        {
            Name = "blocks",
            Type = FieldType.Bytes,
            Repeat = new RepeatMode.LengthPrefixed(1),
        });
        // [02][AA BB] — no terminator, EOF
        var data = new byte[] { 0x02, 0xAA, 0xBB };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(1);
        var block = array.Elements[0].Should().BeOfType<DecodedBytes>().Subject;
        block.RawBytes.ToArray().Should().Equal(0xAA, 0xBB);
    }

    [Fact]
    public void GifStyleSubBlocks_DecodesCorrectly()
    {
        var format = CreateFormat(new FieldDefinition
        {
            Name = "sub_blocks",
            Type = FieldType.Bytes,
            Repeat = new RepeatMode.LengthPrefixed(1),
        });
        // GIF-style: [05][01 02 03 04 05][03][06 07 08][00]
        var data = new byte[]
        {
            0x05, 0x01, 0x02, 0x03, 0x04, 0x05,
            0x03, 0x06, 0x07, 0x08,
            0x00,
        };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);

        var b1 = array.Elements[0].Should().BeOfType<DecodedBytes>().Subject;
        b1.RawBytes.ToArray().Should().Equal(0x01, 0x02, 0x03, 0x04, 0x05);
        b1.Size.Should().Be(5);

        var b2 = array.Elements[1].Should().BeOfType<DecodedBytes>().Subject;
        b2.RawBytes.ToArray().Should().Equal(0x06, 0x07, 0x08);
        b2.Size.Should().Be(3);
    }

    [Fact]
    public void OffsetAndSize_AreAccurate()
    {
        var format = CreateFormat(new FieldDefinition
        {
            Name = "blocks",
            Type = FieldType.Bytes,
            Repeat = new RepeatMode.LengthPrefixed(1),
        });
        // [02][AA BB][01][CC][00]
        var data = new byte[] { 0x02, 0xAA, 0xBB, 0x01, 0xCC, 0x00 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);

        var b1 = array.Elements[0].Should().BeOfType<DecodedBytes>().Subject;
        b1.Offset.Should().Be(1);  // after prefix byte
        b1.Size.Should().Be(2);

        var b2 = array.Elements[1].Should().BeOfType<DecodedBytes>().Subject;
        b2.Offset.Should().Be(4);  // after first block (3 bytes) + second prefix (1 byte)
        b2.Size.Should().Be(1);
    }

    [Fact]
    public void PrefixSize3_BigEndian_DecodesCorrectly()
    {
        var format = CreateFormat(Endianness.Big, new FieldDefinition
        {
            Name = "blocks",
            Type = FieldType.Bytes,
            Repeat = new RepeatMode.LengthPrefixed(3),
        });
        // prefix=0x000002 (BE: 00 00 02), data=AA BB, terminator=00 00 00
        var data = new byte[] { 0x00, 0x00, 0x02, 0xAA, 0xBB, 0x00, 0x00, 0x00 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(1);
        var block = array.Elements[0].Should().BeOfType<DecodedBytes>().Subject;
        block.RawBytes.ToArray().Should().Equal(0xAA, 0xBB);
    }

    private static FormatDefinition CreateFormat(params FieldDefinition[] fields)
        => CreateFormat(Endianness.Big, fields);

    private static FormatDefinition CreateFormat(Endianness endianness, params FieldDefinition[] fields)
    {
        return new FormatDefinition
        {
            Name = "Test",
            Endianness = endianness,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["main"] = new()
                {
                    Name = "main",
                    Fields = fields.ToList(),
                },
            },
            RootStruct = "main",
        };
    }
}
