using BinAnalyzer.Core;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class BitstreamDecoderTests
{
    private static FormatDefinition CreateBitstreamFormat(params FieldDefinition[] fields)
    {
        return new FormatDefinition
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
                    IsBitstream = true,
                    Fields = fields.ToList(),
                },
            },
            RootStruct = "root",
        };
    }

    [Fact]
    public void Decode_ByteAligned_16plus16()
    {
        // 16 bits + 16 bits = 4 bytes, byte-aligned
        var format = CreateBitstreamFormat(
            new FieldDefinition { Name = "a", Type = FieldType.UInt16, Size = 16 },
            new FieldDefinition { Name = "b", Type = FieldType.UInt16, Size = 16 }
        );

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0x12, 0x34, 0x56, 0x78 }, format);

        result.Children.Should().HaveCount(2);
        var a = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        a.Value.Should().Be(0x1234);
        a.BitOffset.Should().Be(0);

        var b = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        b.Value.Should().Be(0x5678);
        b.BitOffset.Should().Be(0);
    }

    [Fact]
    public void Decode_CrossByteBoundary_20plus12()
    {
        // 20 bits + 12 bits = 32 bits = 4 bytes
        // Data: 0xABCDE (20 bits) | 0xF12 (12 bits)
        // Binary: 1010_1011_1100_1101_1110_1111_0001_0010
        // = 0xAB, 0xCD, 0xEF, 0x12
        var format = CreateBitstreamFormat(
            new FieldDefinition { Name = "a", Type = FieldType.UInt32, Size = 20 },
            new FieldDefinition { Name = "b", Type = FieldType.UInt16, Size = 12 }
        );

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0xAB, 0xCD, 0xEF, 0x12 }, format);

        var a = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        a.Value.Should().Be(0xABCDE); // top 20 bits
        a.BitOffset.Should().Be(0);

        var b = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        b.Value.Should().Be(0xF12); // bottom 12 bits
        b.BitOffset.Should().Be(4); // starts at bit 4 within byte
    }

    [Fact]
    public void Decode_SmallBitWidths_3plus5()
    {
        // 3 bits + 5 bits = 8 bits = 1 byte
        // 0xA5 = 1010_0101
        // first 3 bits: 101 = 5
        // next 5 bits: 00101 = 5
        var format = CreateBitstreamFormat(
            new FieldDefinition { Name = "a", Type = FieldType.UInt8, Size = 3 },
            new FieldDefinition { Name = "b", Type = FieldType.UInt8, Size = 5 }
        );

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0xA5 }, format);

        var a = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        a.Value.Should().Be(5); // 101 = 5
        a.BitOffset.Should().Be(0);

        var b = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        b.Value.Should().Be(5); // 00101 = 5
        b.BitOffset.Should().Be(3);
    }

    [Fact]
    public void Decode_SignedInteger_SignExtension()
    {
        // 5-bit signed integer: 11111 = -1
        // 0xF8 = 1111_1000
        // first 5 bits: 11111 = -1 (signed)
        // next 3 bits: 000 = 0
        var format = CreateBitstreamFormat(
            new FieldDefinition { Name = "a", Type = FieldType.Int8, Size = 5 },
            new FieldDefinition { Name = "b", Type = FieldType.UInt8, Size = 3 }
        );

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0xF8 }, format);

        var a = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        a.Value.Should().Be(-1); // 11111 sign-extended to -1

        var b = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        b.Value.Should().Be(0);
    }

    [Fact]
    public void Decode_AutoByteAlign_OnStructExit()
    {
        // 5 bits in a bitstream struct, should auto-align to next byte boundary
        // Then the parent struct continues reading from byte boundary
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
                            Name = "bits",
                            Type = FieldType.Struct,
                            StructRef = "bit_struct",
                        },
                        new FieldDefinition
                        {
                            Name = "next_byte",
                            Type = FieldType.UInt8,
                        },
                    ],
                },
                ["bit_struct"] = new()
                {
                    Name = "bit_struct",
                    IsBitstream = true,
                    Fields =
                    [
                        new FieldDefinition { Name = "val", Type = FieldType.UInt8, Size = 5 },
                    ],
                },
            },
            RootStruct = "root",
        };

        var decoder = new BinaryDecoder();
        // byte 0: 5 bits consumed, 3 bits skipped (auto-align)
        // byte 1: next_byte
        var result = decoder.Decode(new byte[] { 0xFF, 0x42 }, format);

        result.Children.Should().HaveCount(2);
        var bitStruct = result.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        var val = bitStruct.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        val.Value.Should().Be(0x1F); // 11111 = 31

        var nextByte = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        nextByte.Value.Should().Be(0x42);
    }

    [Fact]
    public void Decode_FlacStreaminfoPattern()
    {
        // FLAC STREAMINFO bitstream: 16+16+24+24+20+3+5+36 = 144 bits = 18 bytes
        // min_block_size=4096 (16 bits)
        // max_block_size=4096 (16 bits)
        // min_frame_size=14 (24 bits)
        // max_frame_size=14 (24 bits)
        // sample_rate=44100 (20 bits)
        // channels=1 (3 bits, stored as channels-1, so 1=stereo)
        // bps=15 (5 bits, stored as bps-1, so 15=16-bit)
        // total_samples=0 (36 bits)

        var format = CreateBitstreamFormat(
            new FieldDefinition { Name = "min_block_size", Type = FieldType.UInt16, Size = 16 },
            new FieldDefinition { Name = "max_block_size", Type = FieldType.UInt16, Size = 16 },
            new FieldDefinition { Name = "min_frame_size", Type = FieldType.UInt32, Size = 24 },
            new FieldDefinition { Name = "max_frame_size", Type = FieldType.UInt32, Size = 24 },
            new FieldDefinition { Name = "sample_rate", Type = FieldType.UInt32, Size = 20 },
            new FieldDefinition { Name = "channels", Type = FieldType.UInt8, Size = 3 },
            new FieldDefinition { Name = "bps", Type = FieldType.UInt8, Size = 5 },
            new FieldDefinition { Name = "total_samples", Type = FieldType.UInt64, Size = 36 }
        );

        // min_block_size = 4096 = 0x1000
        // max_block_size = 4096 = 0x1000
        // min_frame_size = 14 = 0x00000E
        // max_frame_size = 14 = 0x00000E
        // sample_rate = 44100 = 0xAC44
        // channels = 1 = 0b001
        // bps = 15 = 0b01111
        // total_samples = 0
        //
        // Bit layout (MSB-first):
        // Bytes 0-1:  min_block_size = 0x10, 0x00
        // Bytes 2-3:  max_block_size = 0x10, 0x00
        // Bytes 4-6:  min_frame_size = 0x00, 0x00, 0x0E
        // Bytes 7-9:  max_frame_size = 0x00, 0x00, 0x0E
        // Bytes 10-12: sample_rate(20 bits) + channels(3 bits) + bps_high(1 bit)
        //   sample_rate = 44100 = 0xAC44 = 0b 1010_1100_0100_0100
        //   20 bits: 0000_1010_1100_0100_0100
        //   channels = 001
        //   bps = 01111
        //   → 0000_1010_1100_0100_0100_001_01111_... + total_samples(36 bits of 0)
        //   byte 10: 0000_1010 = 0x0A
        //   byte 11: 1100_0100 = 0xC4
        //   byte 12: 0100_0010 = 0x42 (sample_rate[3:0]=0100, channels=001, bps[4]=0)
        //   byte 13: 1111_0000 = 0xF0 (bps[3:0]=1111, total_samples[35:32]=0000)
        //   bytes 14-17: 0x00, 0x00, 0x00, 0x00 (total_samples[31:0])

        var data = new byte[]
        {
            0x10, 0x00, // min_block_size
            0x10, 0x00, // max_block_size
            0x00, 0x00, 0x0E, // min_frame_size
            0x00, 0x00, 0x0E, // max_frame_size
            0x0A, 0xC4, 0x42, 0xF0, // sample_rate + channels + bps + total_samples high
            0x00, 0x00, 0x00, 0x00, // total_samples low
        };

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        result.Children.Should().HaveCount(8);

        var minBlock = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        minBlock.Value.Should().Be(4096);

        var maxBlock = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        maxBlock.Value.Should().Be(4096);

        var minFrame = result.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        minFrame.Value.Should().Be(14);

        var maxFrame = result.Children[3].Should().BeOfType<DecodedInteger>().Subject;
        maxFrame.Value.Should().Be(14);

        var sampleRate = result.Children[4].Should().BeOfType<DecodedInteger>().Subject;
        sampleRate.Value.Should().Be(44100);

        var channels = result.Children[5].Should().BeOfType<DecodedInteger>().Subject;
        channels.Value.Should().Be(1);

        var bps = result.Children[6].Should().BeOfType<DecodedInteger>().Subject;
        bps.Value.Should().Be(15);

        var totalSamples = result.Children[7].Should().BeOfType<DecodedInteger>().Subject;
        totalSamples.Value.Should().Be(0);
    }

    [Fact]
    public void Decode_VariableReference_InBitstreamFields()
    {
        // Verify that bitstream fields can be referenced by expression in subsequent fields
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
                    IsBitstream = true,
                    Fields =
                    [
                        new FieldDefinition { Name = "width", Type = FieldType.UInt8, Size = 4 },
                        new FieldDefinition
                        {
                            Name = "value",
                            Type = FieldType.UInt32,
                            SizeExpression = ExpressionParser.Parse("{width}"),
                        },
                    ],
                },
            },
            RootStruct = "root",
        };

        // width = 4 bits → first 4 bits
        // value = {width} bits → next 'width' bits
        // Data: 0100_1010_xxxx_xxxx
        //       width=4(0100), value=next 4 bits = 1010 = 10
        // 0x4A = 0100_1010
        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0x4A }, format);

        result.Children.Should().HaveCount(2);
        var width = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        width.Value.Should().Be(4);

        var value = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        value.Value.Should().Be(10); // 1010 = 10
    }

    [Fact]
    public void Decode_SingleBit()
    {
        // Read a single bit
        var format = CreateBitstreamFormat(
            new FieldDefinition { Name = "flag", Type = FieldType.UInt8, Size = 1 },
            new FieldDefinition { Name = "rest", Type = FieldType.UInt8, Size = 7 }
        );

        // 0x80 = 1000_0000
        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0x80 }, format);

        var flag = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        flag.Value.Should().Be(1);

        var rest = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        rest.Value.Should().Be(0);
    }

    [Fact]
    public void Decode_SignedPositiveValue()
    {
        // 5-bit signed integer with positive value: 01010 = 10
        // 0x50 = 0101_0000
        var format = CreateBitstreamFormat(
            new FieldDefinition { Name = "a", Type = FieldType.Int8, Size = 5 },
            new FieldDefinition { Name = "b", Type = FieldType.UInt8, Size = 3 }
        );

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0x50 }, format);

        var a = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        a.Value.Should().Be(10); // 01010 = 10, positive
    }

    [Fact]
    public void Decode_BitOffset_TrackedCorrectly()
    {
        // Verify BitOffset values across fields
        var format = CreateBitstreamFormat(
            new FieldDefinition { Name = "a", Type = FieldType.UInt8, Size = 3 },
            new FieldDefinition { Name = "b", Type = FieldType.UInt8, Size = 4 },
            new FieldDefinition { Name = "c", Type = FieldType.UInt8, Size = 1 }
        );

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0xFF }, format);

        var a = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        a.BitOffset.Should().Be(0);

        var b = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        b.BitOffset.Should().Be(3);

        var c = result.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        c.BitOffset.Should().Be(7);
    }

    // --- REQ-145: 制御フロー ---

    private static FormatDefinition CreateMultiStructBitstreamFormat(
        Dictionary<string, StructDefinition> structs, string root = "root")
    {
        return new FormatDefinition
        {
            Name = "test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = structs,
            RootStruct = root,
        };
    }

    [Fact]
    public void IfCondition_SkipsField_InBitstream()
    {
        // 1bit flag=0 → 4bit field skipped → 3bit rest
        // 0_101_xxxx → flag=0, rest(3bit)=101=5
        var format = CreateBitstreamFormat(
            new FieldDefinition { Name = "flag", Type = FieldType.UInt8, Size = 1 },
            new FieldDefinition
            {
                Name = "optional_val", Type = FieldType.UInt8, Size = 4,
                Condition = ExpressionParser.Parse("{flag == 1}"),
            },
            new FieldDefinition { Name = "rest", Type = FieldType.UInt8, Size = 3 }
        );

        var decoder = new BinaryDecoder();
        // 0_101_xxxx = 0101_xxxx → 0x50
        var result = decoder.Decode(new byte[] { 0x50 }, format);

        result.Children.Should().HaveCount(2); // flag + rest (optional_val skipped)
        var flag = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        flag.Value.Should().Be(0);

        var rest = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        rest.Value.Should().Be(5); // 101 = 5
    }

    [Fact]
    public void IfCondition_IncludesField_InBitstream()
    {
        // 1bit flag=1 → 4bit field included → 3bit rest
        // 1_1010_011 = 1101_0011 = 0xD3
        var format = CreateBitstreamFormat(
            new FieldDefinition { Name = "flag", Type = FieldType.UInt8, Size = 1 },
            new FieldDefinition
            {
                Name = "optional_val", Type = FieldType.UInt8, Size = 4,
                Condition = ExpressionParser.Parse("{flag == 1}"),
            },
            new FieldDefinition { Name = "rest", Type = FieldType.UInt8, Size = 3 }
        );

        var decoder = new BinaryDecoder();
        // 1_1010_011 = 1101_0011 = 0xD3
        var result = decoder.Decode(new byte[] { 0xD3 }, format);

        result.Children.Should().HaveCount(3); // flag + optional_val + rest
        var flag = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        flag.Value.Should().Be(1);

        var optVal = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        optVal.Value.Should().Be(0b1010); // 1010 = 10

        var rest = result.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        rest.Value.Should().Be(0b011); // 011 = 3
    }

    [Fact]
    public void RepeatCount_IntegerFields_InBitstream()
    {
        // 4bit × 3 = 12 bits = 1.5 bytes → need 2 bytes
        // Values: 0xA=1010, 0xB=1011, 0xC=1100
        // 1010_1011_1100_xxxx → 0xAB, 0xCx
        var format = CreateBitstreamFormat(
            new FieldDefinition
            {
                Name = "items", Type = FieldType.UInt8, Size = 4,
                Repeat = new RepeatMode.Count(ExpressionParser.Parse("3")),
            }
        );

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0xAB, 0xC0 }, format);

        // repeat produces an array node
        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);

        var v0 = array.Elements[0].Should().BeOfType<DecodedInteger>().Subject;
        v0.Value.Should().Be(0xA);

        var v1 = array.Elements[1].Should().BeOfType<DecodedInteger>().Subject;
        v1.Value.Should().Be(0xB);

        var v2 = array.Elements[2].Should().BeOfType<DecodedInteger>().Subject;
        v2.Value.Should().Be(0xC);
    }

    [Fact]
    public void Switch_SelectsBitstreamStruct()
    {
        // 2bit selector → switch to bitstream struct
        // selector=01 → case1 (reads 6 bits)
        // 01_110100 = 0111_0100 = 0x74
        var structs = new Dictionary<string, StructDefinition>
        {
            ["root"] = new()
            {
                Name = "root",
                IsBitstream = true,
                Fields =
                [
                    new FieldDefinition { Name = "selector", Type = FieldType.UInt8, Size = 2 },
                    new FieldDefinition
                    {
                        Name = "body",
                        Type = FieldType.Switch,
                        SwitchOn = ExpressionParser.Parse("{selector}"),
                        SwitchCases =
                        [
                            new SwitchCase(ExpressionParser.Parse("{0}"), "case0"),
                            new SwitchCase(ExpressionParser.Parse("{1}"), "case1"),
                        ],
                        SwitchDefault = "case0",
                    },
                ],
            },
            ["case0"] = new()
            {
                Name = "case0",
                IsBitstream = true,
                Fields = [new FieldDefinition { Name = "a", Type = FieldType.UInt8, Size = 6 }],
            },
            ["case1"] = new()
            {
                Name = "case1",
                IsBitstream = true,
                Fields = [new FieldDefinition { Name = "b", Type = FieldType.UInt8, Size = 6 }],
            },
        };

        var format = CreateMultiStructBitstreamFormat(structs);
        var decoder = new BinaryDecoder();
        // 01_110100 = 0111_0100 = 0x74
        var result = decoder.Decode(new byte[] { 0x74 }, format);

        result.Children.Should().HaveCount(2);
        var selector = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        selector.Value.Should().Be(1);

        var body = result.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var b = body.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        b.Value.Should().Be(0b110100); // 110100 = 52
    }

    [Fact]
    public void Virtual_ComputedValue_InBitstream()
    {
        // 4bit value, virtual computed, 4bit rest
        // 1010_0110 = 0xA6
        var format = CreateBitstreamFormat(
            new FieldDefinition { Name = "a", Type = FieldType.UInt8, Size = 4 },
            new FieldDefinition
            {
                Name = "computed",
                Type = FieldType.Virtual,
                ValueExpression = ExpressionParser.Parse("{a * 2}"),
            },
            new FieldDefinition { Name = "b", Type = FieldType.UInt8, Size = 4 }
        );

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0xA6 }, format);

        result.Children.Should().HaveCount(3);

        var a = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        a.Value.Should().Be(0xA); // 1010 = 10

        var computed = result.Children[1].Should().BeOfType<DecodedVirtual>().Subject;
        computed.Value.Should().Be(20L); // 10 * 2

        // Virtual in bitstream should have BitOffset
        computed.BitOffset.Should().NotBeNull();

        var b = result.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        b.Value.Should().Be(0x6); // 0110 = 6
    }

    [Fact]
    public void NestedBitstreamStruct_ContinuesBitPosition()
    {
        // Parent bitstream: 4bit header, then nested bitstream struct with 4bit field
        // Total: 8 bits = 1 byte
        // 1010_0110 = 0xA6
        var structs = new Dictionary<string, StructDefinition>
        {
            ["root"] = new()
            {
                Name = "root",
                IsBitstream = true,
                Fields =
                [
                    new FieldDefinition { Name = "header", Type = FieldType.UInt8, Size = 4 },
                    new FieldDefinition
                    {
                        Name = "nested",
                        Type = FieldType.Struct,
                        StructRef = "inner",
                    },
                ],
            },
            ["inner"] = new()
            {
                Name = "inner",
                IsBitstream = true,
                Fields =
                [
                    new FieldDefinition { Name = "val", Type = FieldType.UInt8, Size = 4 },
                ],
            },
        };

        var format = CreateMultiStructBitstreamFormat(structs);
        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0xA6 }, format);

        result.Children.Should().HaveCount(2);

        var header = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        header.Value.Should().Be(0xA); // 1010 = 10

        var nested = result.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var val = nested.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        val.Value.Should().Be(0x6); // 0110 = 6 (continues from bit 4)
        val.BitOffset.Should().Be(4); // starts at bit 4
    }

    [Fact]
    public void BitOffset_CorrectAfterControlFlow()
    {
        // 1bit flag, virtual, 3bit value → bit offsets should be consistent
        // 1_101_xxxx = 0xD0
        var format = CreateBitstreamFormat(
            new FieldDefinition { Name = "flag", Type = FieldType.UInt8, Size = 1 },
            new FieldDefinition
            {
                Name = "computed",
                Type = FieldType.Virtual,
                ValueExpression = ExpressionParser.Parse("{flag + 10}"),
            },
            new FieldDefinition { Name = "val", Type = FieldType.UInt8, Size = 3 }
        );

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0xD0 }, format);

        result.Children.Should().HaveCount(3);

        var flag = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        flag.BitOffset.Should().Be(0);
        flag.Value.Should().Be(1);

        var computed = result.Children[1].Should().BeOfType<DecodedVirtual>().Subject;
        computed.BitOffset.Should().Be(1); // after 1 bit
        computed.Value.Should().Be(11L);

        var val = result.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        val.BitOffset.Should().Be(1); // virtual doesn't consume bits
        val.Value.Should().Be(0b101); // 101 = 5
    }

    // --- REQ-153: LSB-first bit order ---

    private static FormatDefinition CreateLsbBitstreamFormat(params FieldDefinition[] fields)
    {
        return new FormatDefinition
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
                    IsBitstream = true,
                    BitOrder = BitOrder.Lsb,
                    Fields = fields.ToList(),
                },
            },
            RootStruct = "root",
        };
    }

    [Fact]
    public void LsbFirstReadsLowBitsFirst()
    {
        // 0xA5 = 10100101
        // LSB-first, read 4 bits: bits 0-3 = 0101 = 5
        var format = CreateLsbBitstreamFormat(
            new FieldDefinition { Name = "a", Type = FieldType.UInt8, Size = 4 }
        );

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0xA5 }, format);

        var a = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        a.Value.Should().Be(5); // 0101 = 5 (low nibble of 0xA5)
    }

    [Fact]
    public void LsbFirstMultiByteField()
    {
        // Two bytes: 0x3F, 0xC0
        // Binary: 00111111 11000000
        // LSB-first: read 12 bits across byte boundary
        // Byte 0: bits 0-7 = 00111111 → all 8 bits = 0x3F
        // Byte 1: bits 0-3 = 0000 → 4 bits = 0
        // result = byte0_8bits | (byte1_4bits << 8) = 0x3F | (0x00 << 8) = 0x03F
        var format = CreateLsbBitstreamFormat(
            new FieldDefinition { Name = "a", Type = FieldType.UInt16, Size = 12 }
        );

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0x3F, 0xC0 }, format);

        var a = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        // 0x3F = 00111111, first 8 bits (LSB) → 0x3F
        // 0xC0 = 11000000, next 4 bits (LSB) → 0000 = 0
        // result = 0x3F | (0 << 8) = 0x03F = 63
        a.Value.Should().Be(0x03F);
    }

    [Fact]
    public void MsbFirstExplicitSameAsDefault()
    {
        // bit_order: msb (explicit) should behave same as default (null)
        var formatExplicit = new FormatDefinition
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
                    IsBitstream = true,
                    BitOrder = BitOrder.Msb,
                    Fields =
                    [
                        new FieldDefinition { Name = "a", Type = FieldType.UInt8, Size = 4 },
                        new FieldDefinition { Name = "b", Type = FieldType.UInt8, Size = 4 },
                    ],
                },
            },
            RootStruct = "root",
        };

        var formatDefault = CreateBitstreamFormat(
            new FieldDefinition { Name = "a", Type = FieldType.UInt8, Size = 4 },
            new FieldDefinition { Name = "b", Type = FieldType.UInt8, Size = 4 }
        );

        var data = new byte[] { 0xA5 };
        var decoder = new BinaryDecoder();

        var resultExplicit = decoder.Decode(data, formatExplicit);
        var resultDefault = decoder.Decode(data, formatDefault);

        var aExplicit = resultExplicit.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        var aDefault = resultDefault.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        aExplicit.Value.Should().Be(aDefault.Value);

        var bExplicit = resultExplicit.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        var bDefault = resultDefault.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        bExplicit.Value.Should().Be(bDefault.Value);
    }

    [Fact]
    public void LsbFirstSequentialReads()
    {
        // 0xA5 = 10100101
        // LSB-first sequential reads:
        // read 3 bits: bits 0-2 = 101 = 5
        // read 3 bits: bits 3-5 = 100 = 4
        // read 2 bits: bits 6-7 = 10 = 2
        var format = CreateLsbBitstreamFormat(
            new FieldDefinition { Name = "a", Type = FieldType.UInt8, Size = 3 },
            new FieldDefinition { Name = "b", Type = FieldType.UInt8, Size = 3 },
            new FieldDefinition { Name = "c", Type = FieldType.UInt8, Size = 2 }
        );

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0xA5 }, format);

        var a = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        a.Value.Should().Be(5); // bits[2:0] = 101 = 5

        var b = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        b.Value.Should().Be(4); // bits[5:3] = 100 = 4

        var c = result.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        c.Value.Should().Be(2); // bits[7:6] = 10 = 2
    }
}
