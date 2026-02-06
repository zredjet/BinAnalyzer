using System.Buffers.Binary;
using System.Text;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class ChecksumDecoderTests
{
    [Fact]
    public void Decode_ValidChecksum_ReturnsValid()
    {
        // type(4 bytes, ascii "TEST") + crc(4 bytes, uint32)
        // CRC-32 of "TEST" = Crc32Calculator.Compute(...)
        var typeBytes = Encoding.ASCII.GetBytes("TEST");
        var expectedCrc = Crc32Calculator.Compute(typeBytes);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, expectedCrc);

        var data = new byte[typeBytes.Length + crcBytes.Length];
        typeBytes.CopyTo(data, 0);
        crcBytes.CopyTo(data, typeBytes.Length);

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
                        new FieldDefinition { Name = "type", Type = FieldType.Ascii, Size = 4 },
                        new FieldDefinition
                        {
                            Name = "crc",
                            Type = FieldType.UInt32,
                            Checksum = new ChecksumSpec
                            {
                                Algorithm = "crc32",
                                FieldNames = ["type"],
                            },
                        },
                    ],
                },
            },
            RootStruct = "root",
        };

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var crcNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        crcNode.ChecksumValid.Should().BeTrue();
        crcNode.ChecksumExpected.Should().BeNull();
    }

    [Fact]
    public void Decode_InvalidChecksum_ReturnsInvalidWithExpected()
    {
        var typeBytes = Encoding.ASCII.GetBytes("TEST");
        var wrongCrc = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        var data = new byte[typeBytes.Length + wrongCrc.Length];
        typeBytes.CopyTo(data, 0);
        wrongCrc.CopyTo(data, typeBytes.Length);

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
                        new FieldDefinition { Name = "type", Type = FieldType.Ascii, Size = 4 },
                        new FieldDefinition
                        {
                            Name = "crc",
                            Type = FieldType.UInt32,
                            Checksum = new ChecksumSpec
                            {
                                Algorithm = "crc32",
                                FieldNames = ["type"],
                            },
                        },
                    ],
                },
            },
            RootStruct = "root",
        };

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var crcNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        crcNode.ChecksumValid.Should().BeFalse();
        crcNode.ChecksumExpected.Should().NotBeNull();

        // 期待値は "TEST" の CRC-32
        var expected = (long)Crc32Calculator.Compute(typeBytes);
        crcNode.ChecksumExpected.Should().Be(expected);
    }

    [Fact]
    public void Decode_MultipleFieldsChecksum_ComputesOverAll()
    {
        // field1(2 bytes) + field2(2 bytes) + crc(4 bytes)
        var field1Data = new byte[] { 0x01, 0x02 };
        var field2Data = new byte[] { 0x03, 0x04 };
        var combined = new byte[field1Data.Length + field2Data.Length];
        field1Data.CopyTo(combined, 0);
        field2Data.CopyTo(combined, field1Data.Length);
        var expectedCrc = Crc32Calculator.Compute(combined);

        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, expectedCrc);

        var data = new byte[field1Data.Length + field2Data.Length + crcBytes.Length];
        field1Data.CopyTo(data, 0);
        field2Data.CopyTo(data, field1Data.Length);
        crcBytes.CopyTo(data, field1Data.Length + field2Data.Length);

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
                        new FieldDefinition { Name = "f1", Type = FieldType.UInt16 },
                        new FieldDefinition { Name = "f2", Type = FieldType.UInt16 },
                        new FieldDefinition
                        {
                            Name = "crc",
                            Type = FieldType.UInt32,
                            Checksum = new ChecksumSpec
                            {
                                Algorithm = "crc32",
                                FieldNames = ["f1", "f2"],
                            },
                        },
                    ],
                },
            },
            RootStruct = "root",
        };

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var crcNode = result.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        crcNode.ChecksumValid.Should().BeTrue();
    }

    [Fact]
    public void Decode_FieldWithoutChecksum_HasNullProperties()
    {
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
                        new FieldDefinition { Name = "value", Type = FieldType.UInt32 },
                    ],
                },
            },
            RootStruct = "root",
        };

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(new byte[] { 0, 0, 0, 42 }, format);

        var intNode = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        intNode.ChecksumValid.Should().BeNull();
        intNode.ChecksumExpected.Should().BeNull();
    }
}
