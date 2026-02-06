using System.Buffers.Binary;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class BitfieldDecoderTests
{
    private static FormatDefinition CreateBitfieldFormat(
        int size,
        List<BitfieldEntry> entries,
        Dictionary<string, EnumDefinition>? enums = null)
    {
        return new FormatDefinition
        {
            Name = "test",
            Endianness = Endianness.Big,
            Enums = enums ?? new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["root"] = new()
                {
                    Name = "root",
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "flags",
                            Type = FieldType.Bitfield,
                            Size = size,
                            BitfieldEntries = entries,
                        },
                    ],
                },
            },
            RootStruct = "root",
        };
    }

    [Fact]
    public void Decode_SingleBit_ExtractsCorrectly()
    {
        // 0xA5 = 10100101
        var format = CreateBitfieldFormat(1,
        [
            new BitfieldEntry { Name = "bit7", BitHigh = 7, BitLow = 7 },
            new BitfieldEntry { Name = "bit0", BitHigh = 0, BitLow = 0 },
        ]);

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0xA5 }, format);

        var bitfield = result.Children[0].Should().BeOfType<DecodedBitfield>().Subject;
        bitfield.RawValue.Should().Be(0xA5);
        bitfield.Fields.Should().HaveCount(2);
        bitfield.Fields[0].Name.Should().Be("bit7");
        bitfield.Fields[0].Value.Should().Be(1);
        bitfield.Fields[1].Name.Should().Be("bit0");
        bitfield.Fields[1].Value.Should().Be(1);
    }

    [Fact]
    public void Decode_BitRange_ExtractsCorrectly()
    {
        // 0xA5 = 1010_0101
        // bits 7:4 = 1010 = 10
        // bits 3:0 = 0101 = 5
        var format = CreateBitfieldFormat(1,
        [
            new BitfieldEntry { Name = "high", BitHigh = 7, BitLow = 4 },
            new BitfieldEntry { Name = "low", BitHigh = 3, BitLow = 0 },
        ]);

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0xA5 }, format);

        var bitfield = result.Children[0].Should().BeOfType<DecodedBitfield>().Subject;
        bitfield.Fields[0].Value.Should().Be(10); // 0xA
        bitfield.Fields[1].Value.Should().Be(5);  // 0x5
    }

    [Fact]
    public void Decode_TwoBytes_BigEndian()
    {
        // 0x1234 = 0001_0010_0011_0100
        // bits 15:8 = 0x12 = 18
        // bits 7:0 = 0x34 = 52
        var format = CreateBitfieldFormat(2,
        [
            new BitfieldEntry { Name = "high_byte", BitHigh = 15, BitLow = 8 },
            new BitfieldEntry { Name = "low_byte", BitHigh = 7, BitLow = 0 },
        ]);

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0x12, 0x34 }, format);

        var bitfield = result.Children[0].Should().BeOfType<DecodedBitfield>().Subject;
        bitfield.RawValue.Should().Be(0x1234);
        bitfield.Fields[0].Value.Should().Be(0x12);
        bitfield.Fields[1].Value.Should().Be(0x34);
    }

    [Fact]
    public void Decode_FourBytes_BigEndian()
    {
        // 0xDEADBEEF
        var data = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(data, 0xDEADBEEF);

        var format = CreateBitfieldFormat(4,
        [
            new BitfieldEntry { Name = "top", BitHigh = 31, BitLow = 24 },
            new BitfieldEntry { Name = "bottom", BitHigh = 7, BitLow = 0 },
        ]);

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var bitfield = result.Children[0].Should().BeOfType<DecodedBitfield>().Subject;
        bitfield.RawValue.Should().Be(unchecked((long)0xDEADBEEF));
        bitfield.Fields[0].Value.Should().Be(0xDE);
        bitfield.Fields[1].Value.Should().Be(0xEF);
    }

    [Fact]
    public void Decode_WithEnumRef_ResolvesLabel()
    {
        var format = CreateBitfieldFormat(1,
        [
            new BitfieldEntry { Name = "mode", BitHigh = 1, BitLow = 0, EnumRef = "mode_type" },
        ],
        enums: new Dictionary<string, EnumDefinition>
        {
            ["mode_type"] = new()
            {
                Name = "mode_type",
                Entries =
                [
                    new EnumEntry(0, "off"),
                    new EnumEntry(1, "normal"),
                    new EnumEntry(2, "turbo"),
                    new EnumEntry(3, "max"),
                ],
            },
        });

        var decoder = new BinaryDecoder();
        // 0x02 = 00000010, bits 1:0 = 2
        var result = decoder.Decode(new byte[] { 0x02 }, format);

        var bitfield = result.Children[0].Should().BeOfType<DecodedBitfield>().Subject;
        bitfield.Fields[0].Value.Should().Be(2);
        bitfield.Fields[0].EnumLabel.Should().Be("turbo");
    }

    [Fact]
    public void Decode_VariableBinding_RawValueAvailable()
    {
        // ビットフィールドの後にサイズ式で参照できることを確認
        var format = new FormatDefinition
        {
            Name = "test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["root"] = new()
                {
                    Name = "root",
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "header",
                            Type = FieldType.Bitfield,
                            Size = 1,
                            BitfieldEntries =
                            [
                                new BitfieldEntry { Name = "flag", BitHigh = 7, BitLow = 7 },
                            ],
                        },
                        new FieldDefinition
                        {
                            Name = "value",
                            Type = FieldType.UInt8,
                        },
                    ],
                },
            },
            RootStruct = "root",
        };

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0x80, 0x42 }, format);

        result.Children.Should().HaveCount(2);
        result.Children[0].Should().BeOfType<DecodedBitfield>();
        var intNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        intNode.Value.Should().Be(0x42);
    }
}
