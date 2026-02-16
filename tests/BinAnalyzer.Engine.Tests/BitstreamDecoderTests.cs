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
}
