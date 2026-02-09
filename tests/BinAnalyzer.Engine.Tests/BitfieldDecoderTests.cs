using System.Buffers.Binary;
using BinAnalyzer.Core;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
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

    [Fact]
    public void Decode_BitfieldEntryValue_AvailableInIfCondition()
    {
        // bitfieldエントリ値がif条件で参照可能なことを確認
        // 0xC0 = 1100_0000 → has_extra(bit7)=1, reserved(bit6)=1
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
                            Name = "flags",
                            Type = FieldType.Bitfield,
                            Size = 1,
                            BitfieldEntries =
                            [
                                new BitfieldEntry { Name = "has_extra", BitHigh = 7, BitLow = 7 },
                                new BitfieldEntry { Name = "data_len", BitHigh = 3, BitLow = 0 },
                            ],
                        },
                        new FieldDefinition
                        {
                            Name = "extra_data",
                            Type = FieldType.UInt8,
                            Condition = ExpressionParser.Parse("{has_extra == 1}"),
                        },
                    ],
                },
            },
            RootStruct = "root",
        };

        var decoder = new BinaryDecoder();
        // 0xC0 = has_extra=1, data_len=0; 0x42 = extra_data
        var result = decoder.Decode(new byte[] { 0xC0, 0x42 }, format);

        result.Children.Should().HaveCount(2);
        var extra = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        extra.Name.Should().Be("extra_data");
        extra.Value.Should().Be(0x42);
    }

    [Fact]
    public void Decode_BitfieldEntryValue_ConditionFalse_FieldSkipped()
    {
        // has_extra=0のときextra_dataがスキップされる
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
                            Name = "flags",
                            Type = FieldType.Bitfield,
                            Size = 1,
                            BitfieldEntries =
                            [
                                new BitfieldEntry { Name = "has_extra", BitHigh = 7, BitLow = 7 },
                            ],
                        },
                        new FieldDefinition
                        {
                            Name = "extra_data",
                            Type = FieldType.UInt8,
                            Condition = ExpressionParser.Parse("{has_extra == 1}"),
                        },
                    ],
                },
            },
            RootStruct = "root",
        };

        var decoder = new BinaryDecoder();
        // 0x00 = has_extra=0 → extra_data skipped
        var result = decoder.Decode(new byte[] { 0x00 }, format);

        result.Children.Should().HaveCount(1);
        result.Children[0].Should().BeOfType<DecodedBitfield>();
    }

    [Fact]
    public void Decode_3ByteBitfield_ExtractsCorrectly()
    {
        // WebP VP8 frame_tag: 3 bytes little-endian
        // 0x10, 0x00, 0x00 = 0x000010
        // bit0 = keyframe = 0
        // bits 3:1 = version = 0
        // bit4 = show_frame = 1
        // bits 23:5 = first_part_size = 0
        var format = new FormatDefinition
        {
            Name = "test",
            Endianness = Endianness.Little,
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
                            Name = "frame_tag",
                            Type = FieldType.Bitfield,
                            Size = 3,
                            BitfieldEntries =
                            [
                                new BitfieldEntry { Name = "keyframe", BitHigh = 0, BitLow = 0 },
                                new BitfieldEntry { Name = "version", BitHigh = 3, BitLow = 1 },
                                new BitfieldEntry { Name = "show_frame", BitHigh = 4, BitLow = 4 },
                                new BitfieldEntry { Name = "first_part_size", BitHigh = 23, BitLow = 5 },
                            ],
                        },
                    ],
                },
            },
            RootStruct = "root",
        };

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0x10, 0x00, 0x00 }, format);

        var bitfield = result.Children[0].Should().BeOfType<DecodedBitfield>().Subject;
        bitfield.RawValue.Should().Be(0x000010);
        bitfield.Fields[0].Value.Should().Be(0); // keyframe
        bitfield.Fields[1].Value.Should().Be(0); // version
        bitfield.Fields[2].Value.Should().Be(1); // show_frame
        bitfield.Fields[3].Value.Should().Be(0); // first_part_size
    }

    [Fact]
    public void Decode_8ByteBitfield_ExtractsCorrectly()
    {
        // FLAC STREAMINFO: 8 bytes big-endian
        // sample_rate(20 bits) = 44100 = 0x0AC44
        // channels(3 bits) = 1 (stereo, stored as channels-1)
        // bps(5 bits) = 15 (16 bits, stored as bps-1) → 0b01111
        // total_samples(36 bits) = 0
        //
        // Byte 0: sample_rate[19:12] = 0x0A
        // Byte 1: sample_rate[11:4] = 0xC4
        // Byte 2: sample_rate[3:0]=0100 | channels=001 | bps[4]=0 = 0x42
        // Byte 3: bps[3:0]=1111 | total_samples[35:32]=0000 = 0xF0
        // Bytes 4-7: 0x00
        var format = CreateBitfieldFormat(8,
        [
            new BitfieldEntry { Name = "sample_rate", BitHigh = 63, BitLow = 44 },
            new BitfieldEntry { Name = "channels", BitHigh = 43, BitLow = 41 },
            new BitfieldEntry { Name = "bps", BitHigh = 40, BitLow = 36 },
            new BitfieldEntry { Name = "total_samples", BitHigh = 35, BitLow = 0 },
        ]);

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(
            new byte[] { 0x0A, 0xC4, 0x42, 0xF0, 0x00, 0x00, 0x00, 0x00 }, format);

        var bitfield = result.Children[0].Should().BeOfType<DecodedBitfield>().Subject;
        bitfield.Fields[0].Value.Should().Be(44100); // sample_rate
        bitfield.Fields[1].Value.Should().Be(1);     // channels
        bitfield.Fields[2].Value.Should().Be(15);    // bps (=16-1)
        bitfield.Fields[3].Value.Should().Be(0);     // total_samples
    }

    [Fact]
    public void Decode_5ByteBitfield_ExtractsCorrectly()
    {
        // 5 bytes big-endian: 0x01, 0x02, 0x03, 0x04, 0x05
        // = 0x0102030405
        // bits 39:32 = 0x01 = 1
        // bits 7:0 = 0x05 = 5
        var format = CreateBitfieldFormat(5,
        [
            new BitfieldEntry { Name = "high_byte", BitHigh = 39, BitLow = 32 },
            new BitfieldEntry { Name = "low_byte", BitHigh = 7, BitLow = 0 },
        ]);

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(
            new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 }, format);

        var bitfield = result.Children[0].Should().BeOfType<DecodedBitfield>().Subject;
        bitfield.RawValue.Should().Be(0x0102030405L);
        bitfield.Fields[0].Value.Should().Be(0x01);
        bitfield.Fields[1].Value.Should().Be(0x05);
    }

    [Fact]
    public void Decode_InvalidBitfieldSize_Throws()
    {
        var format9 = CreateBitfieldFormat(9,
        [
            new BitfieldEntry { Name = "dummy", BitHigh = 0, BitLow = 0 },
        ]);

        var decoder = new BinaryDecoder();
        var act = () => decoder.Decode(new byte[9], format9);
        act.Should().Throw<DecodeException>()
            .WithMessage("*1-8 bytes*");
    }
}
