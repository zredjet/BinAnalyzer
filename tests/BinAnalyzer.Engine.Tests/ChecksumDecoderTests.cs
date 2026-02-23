using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using BinAnalyzer.Core;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
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
    public void Decode_Crc32_ChecksumAlgorithmPropertySet()
    {
        var typeBytes = Encoding.ASCII.GetBytes("TEST");
        var expectedCrc = Crc32Calculator.Compute(typeBytes);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, expectedCrc);

        var data = new byte[typeBytes.Length + crcBytes.Length];
        typeBytes.CopyTo(data, 0);
        crcBytes.CopyTo(data, typeBytes.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "type", Type = FieldType.Ascii, Size = 4 },
            new FieldDefinition
            {
                Name = "crc",
                Type = FieldType.UInt32,
                Checksum = new ChecksumSpec { Algorithm = "crc32", FieldNames = ["type"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var crcNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        crcNode.ChecksumValid.Should().BeTrue();
        crcNode.ChecksumAlgorithm.Should().Be("crc32");
    }

    [Fact]
    public void Decode_Crc16Ccitt_ValidChecksum()
    {
        var payload = Encoding.ASCII.GetBytes("TEST");
        var expectedCrc = Crc16Calculator.ComputeCcitt(payload);
        var crcBytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(crcBytes, expectedCrc);

        var data = new byte[payload.Length + crcBytes.Length];
        payload.CopyTo(data, 0);
        crcBytes.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 4 },
            new FieldDefinition
            {
                Name = "crc",
                Type = FieldType.UInt16,
                Checksum = new ChecksumSpec { Algorithm = "crc16-ccitt", FieldNames = ["data"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var crcNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        crcNode.ChecksumValid.Should().BeTrue();
        crcNode.ChecksumAlgorithm.Should().Be("crc16-ccitt");
    }

    [Fact]
    public void Decode_Crc16Ibm_ValidChecksum()
    {
        var payload = Encoding.ASCII.GetBytes("TEST");
        var expectedCrc = Crc16Calculator.ComputeIbm(payload);
        var crcBytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(crcBytes, expectedCrc);

        var data = new byte[payload.Length + crcBytes.Length];
        payload.CopyTo(data, 0);
        crcBytes.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 4 },
            new FieldDefinition
            {
                Name = "crc",
                Type = FieldType.UInt16,
                Checksum = new ChecksumSpec { Algorithm = "crc16-ibm", FieldNames = ["data"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var crcNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        crcNode.ChecksumValid.Should().BeTrue();
        crcNode.ChecksumAlgorithm.Should().Be("crc16-ibm");
    }

    [Fact]
    public void Decode_Adler32_ValidChecksum()
    {
        var payload = Encoding.ASCII.GetBytes("TEST");
        var expectedAdler = Adler32Calculator.Compute(payload);
        var adlerBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(adlerBytes, expectedAdler);

        var data = new byte[payload.Length + adlerBytes.Length];
        payload.CopyTo(data, 0);
        adlerBytes.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 4 },
            new FieldDefinition
            {
                Name = "checksum",
                Type = FieldType.UInt32,
                Checksum = new ChecksumSpec { Algorithm = "adler32", FieldNames = ["data"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var checksumNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        checksumNode.ChecksumValid.Should().BeTrue();
        checksumNode.ChecksumAlgorithm.Should().Be("adler32");
    }

    [Fact]
    public void Decode_Md5_ValidChecksum()
    {
        var payload = Encoding.ASCII.GetBytes("TESTDATA");
        var hash = MD5.HashData(payload);

        var data = new byte[payload.Length + hash.Length];
        payload.CopyTo(data, 0);
        hash.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 8 },
            new FieldDefinition
            {
                Name = "hash",
                Type = FieldType.Bytes,
                Size = 16,
                Checksum = new ChecksumSpec { Algorithm = "md5", FieldNames = ["data"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var hashNode = result.Children[1].Should().BeOfType<DecodedBytes>().Subject;
        hashNode.ChecksumValid.Should().BeTrue();
        hashNode.ChecksumExpectedHex.Should().BeNull();
        hashNode.ChecksumAlgorithm.Should().Be("md5");
    }

    [Fact]
    public void Decode_Sha1_ValidChecksum()
    {
        var payload = Encoding.ASCII.GetBytes("TESTDATA");
        var hash = SHA1.HashData(payload);

        var data = new byte[payload.Length + hash.Length];
        payload.CopyTo(data, 0);
        hash.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 8 },
            new FieldDefinition
            {
                Name = "hash",
                Type = FieldType.Bytes,
                Size = 20,
                Checksum = new ChecksumSpec { Algorithm = "sha1", FieldNames = ["data"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var hashNode = result.Children[1].Should().BeOfType<DecodedBytes>().Subject;
        hashNode.ChecksumValid.Should().BeTrue();
        hashNode.ChecksumAlgorithm.Should().Be("sha1");
    }

    [Fact]
    public void Decode_Sha256_ValidChecksum()
    {
        var payload = Encoding.ASCII.GetBytes("TESTDATA");
        var hash = SHA256.HashData(payload);

        var data = new byte[payload.Length + hash.Length];
        payload.CopyTo(data, 0);
        hash.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 8 },
            new FieldDefinition
            {
                Name = "hash",
                Type = FieldType.Bytes,
                Size = 32,
                Checksum = new ChecksumSpec { Algorithm = "sha256", FieldNames = ["data"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var hashNode = result.Children[1].Should().BeOfType<DecodedBytes>().Subject;
        hashNode.ChecksumValid.Should().BeTrue();
        hashNode.ChecksumAlgorithm.Should().Be("sha256");
    }

    [Fact]
    public void Decode_Sha256_InvalidChecksum_ReturnsExpectedHex()
    {
        var payload = Encoding.ASCII.GetBytes("TESTDATA");
        var wrongHash = new byte[32]; // all zeros

        var data = new byte[payload.Length + wrongHash.Length];
        payload.CopyTo(data, 0);
        wrongHash.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 8 },
            new FieldDefinition
            {
                Name = "hash",
                Type = FieldType.Bytes,
                Size = 32,
                Checksum = new ChecksumSpec { Algorithm = "sha256", FieldNames = ["data"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var hashNode = result.Children[1].Should().BeOfType<DecodedBytes>().Subject;
        hashNode.ChecksumValid.Should().BeFalse();
        hashNode.ChecksumExpectedHex.Should().NotBeNullOrEmpty();
        hashNode.ChecksumAlgorithm.Should().Be("sha256");
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

    // --- Range-based checksum tests ---

    [Fact]
    public void Decode_ChecksumWithRange_ComputesCorrectly()
    {
        // data(8 bytes) + crc(4 bytes)
        // CRC-32 of first 8 bytes
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        var expectedCrc = Crc32Calculator.Compute(payload);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, expectedCrc);

        var data = new byte[payload.Length + crcBytes.Length];
        payload.CopyTo(data, 0);
        crcBytes.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Bytes, Size = 8 },
            new FieldDefinition
            {
                Name = "crc",
                Type = FieldType.UInt32,
                Checksum = new ChecksumSpec
                {
                    Algorithm = "crc32",
                    FieldNames = [],
                    Range = new ChecksumRange
                    {
                        OffsetExpression = ExpressionParser.Parse("0"),
                        SizeExpression = ExpressionParser.Parse("8"),
                    },
                },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var crcNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        crcNode.ChecksumValid.Should().BeTrue();
    }

    [Fact]
    public void Decode_ChecksumWithRanges_ConcatenatesRanges()
    {
        // Build data: 4 bytes (range1) + 4 bytes (skip) + 4 bytes (range2) + 4 bytes (crc)
        var range1 = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var skip = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
        var range2 = new byte[] { 0x05, 0x06, 0x07, 0x08 };
        var combined = new byte[range1.Length + range2.Length];
        range1.CopyTo(combined, 0);
        range2.CopyTo(combined, range1.Length);
        var expectedCrc = Crc32Calculator.Compute(combined);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, expectedCrc);

        var data = new byte[range1.Length + skip.Length + range2.Length + crcBytes.Length];
        range1.CopyTo(data, 0);
        skip.CopyTo(data, 4);
        range2.CopyTo(data, 8);
        crcBytes.CopyTo(data, 12);

        var format = MakeFormat(
            new FieldDefinition { Name = "r1", Type = FieldType.Bytes, Size = 4 },
            new FieldDefinition { Name = "skip", Type = FieldType.Bytes, Size = 4 },
            new FieldDefinition { Name = "r2", Type = FieldType.Bytes, Size = 4 },
            new FieldDefinition
            {
                Name = "crc",
                Type = FieldType.UInt32,
                Checksum = new ChecksumSpec
                {
                    Algorithm = "crc32",
                    FieldNames = [],
                    Ranges =
                    [
                        new ChecksumRange
                        {
                            OffsetExpression = ExpressionParser.Parse("0"),
                            SizeExpression = ExpressionParser.Parse("4"),
                        },
                        new ChecksumRange
                        {
                            OffsetExpression = ExpressionParser.Parse("8"),
                            SizeExpression = ExpressionParser.Parse("4"),
                        },
                    ],
                },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var crcNode = result.Children[3].Should().BeOfType<DecodedInteger>().Subject;
        crcNode.ChecksumValid.Should().BeTrue();
    }

    [Fact]
    public void Decode_ChecksumWithExcludeSelf_ZeroFillsOwnBytes()
    {
        // Layout: 12 bytes total, crc at offset 4 (4 bytes)
        // Range covers entire 12 bytes, exclude_self zeros bytes [4..8)
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00, 0x09, 0x0A, 0x0B, 0x0C };
        // compute CRC with crc field zeroed
        var expectedCrc = Crc32Calculator.Compute(payload);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, expectedCrc);

        // Build actual data with CRC filled in
        var data = new byte[12];
        data[0] = 0x01; data[1] = 0x02; data[2] = 0x03; data[3] = 0x04;
        crcBytes.CopyTo(data, 4);
        data[8] = 0x09; data[9] = 0x0A; data[10] = 0x0B; data[11] = 0x0C;

        var format = MakeFormat(
            new FieldDefinition { Name = "header", Type = FieldType.Bytes, Size = 4 },
            new FieldDefinition
            {
                Name = "crc",
                Type = FieldType.UInt32,
                Checksum = new ChecksumSpec
                {
                    Algorithm = "crc32",
                    FieldNames = [],
                    Range = new ChecksumRange
                    {
                        OffsetExpression = ExpressionParser.Parse("0"),
                        SizeExpression = ExpressionParser.Parse("12"),
                    },
                    ExcludeSelf = true,
                },
            },
            new FieldDefinition { Name = "trailer", Type = FieldType.Bytes, Size = 4 });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var crcNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        crcNode.ChecksumValid.Should().BeTrue();
    }

    [Fact]
    public void Decode_ChecksumRangeExceedsBoundary_ThrowsError()
    {
        // Data is only 8 bytes but range asks for 0..100
        var data = new byte[8];

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Bytes, Size = 4 },
            new FieldDefinition
            {
                Name = "crc",
                Type = FieldType.UInt32,
                Checksum = new ChecksumSpec
                {
                    Algorithm = "crc32",
                    FieldNames = [],
                    Range = new ChecksumRange
                    {
                        OffsetExpression = ExpressionParser.Parse("0"),
                        SizeExpression = ExpressionParser.Parse("100"),
                    },
                },
            });

        var decoder = new BinaryDecoder();
        var act = () => decoder.Decode(data, format);
        act.Should().Throw<DecodeException>().WithMessage("*exceeds data boundary*");
    }

    [Fact]
    public void Decode_ChecksumWithRangeExpression_EvaluatesAtDecodeTime()
    {
        // header_size(2 bytes, value=6) + padding(4 bytes) + crc(4 bytes)
        // Range: offset=0, size={header_size} = 6
        var payloadBytes = new byte[] { 0x00, 0x06, 0xAA, 0xBB, 0xCC, 0xDD };
        var expectedCrc = Crc32Calculator.Compute(payloadBytes);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, expectedCrc);

        var data = new byte[payloadBytes.Length + crcBytes.Length];
        payloadBytes.CopyTo(data, 0);
        crcBytes.CopyTo(data, payloadBytes.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "header_size", Type = FieldType.UInt16 },
            new FieldDefinition { Name = "padding", Type = FieldType.Bytes, Size = 4 },
            new FieldDefinition
            {
                Name = "crc",
                Type = FieldType.UInt32,
                Checksum = new ChecksumSpec
                {
                    Algorithm = "crc32",
                    FieldNames = [],
                    Range = new ChecksumRange
                    {
                        OffsetExpression = ExpressionParser.Parse("0"),
                        SizeExpression = ExpressionParser.Parse("{header_size}"),
                    },
                },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var crcNode = result.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        crcNode.ChecksumValid.Should().BeTrue();
    }

    [Fact]
    public void Decode_HashChecksum_WithRange_ComputesCorrectly()
    {
        // data(8 bytes) + sha256(32 bytes)
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        var hash = SHA256.HashData(payload);

        var data = new byte[payload.Length + hash.Length];
        payload.CopyTo(data, 0);
        hash.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Bytes, Size = 8 },
            new FieldDefinition
            {
                Name = "hash",
                Type = FieldType.Bytes,
                Size = 32,
                Checksum = new ChecksumSpec
                {
                    Algorithm = "sha256",
                    FieldNames = [],
                    Range = new ChecksumRange
                    {
                        OffsetExpression = ExpressionParser.Parse("0"),
                        SizeExpression = ExpressionParser.Parse("8"),
                    },
                },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var hashNode = result.Children[1].Should().BeOfType<DecodedBytes>().Subject;
        hashNode.ChecksumValid.Should().BeTrue();
    }

    [Fact]
    public void Decode_ExistingFieldNamesChecksum_StillWorks()
    {
        // Verify existing field-names based checksum still works after changes
        var typeBytes = Encoding.ASCII.GetBytes("TEST");
        var expectedCrc = Crc32Calculator.Compute(typeBytes);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, expectedCrc);

        var data = new byte[typeBytes.Length + crcBytes.Length];
        typeBytes.CopyTo(data, 0);
        crcBytes.CopyTo(data, typeBytes.Length);

        var format = MakeFormat(
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
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var crcNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        crcNode.ChecksumValid.Should().BeTrue();
    }

    // --- New algorithm E2E tests ---

    [Fact]
    public void Decode_Crc8Smbus_ValidChecksum()
    {
        var payload = Encoding.ASCII.GetBytes("TEST");
        byte expectedCrc = Crc8Calculator.ComputeSmbus(payload);

        var data = new byte[payload.Length + 1];
        payload.CopyTo(data, 0);
        data[payload.Length] = expectedCrc;

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 4 },
            new FieldDefinition
            {
                Name = "crc",
                Type = FieldType.UInt8,
                Checksum = new ChecksumSpec { Algorithm = "crc8", FieldNames = ["data"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var crcNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        crcNode.ChecksumValid.Should().BeTrue();
        crcNode.ChecksumAlgorithm.Should().Be("crc8");
    }

    [Fact]
    public void Decode_Crc8Maxim_ValidChecksum()
    {
        var payload = Encoding.ASCII.GetBytes("TEST");
        byte expectedCrc = Crc8Calculator.ComputeMaxim(payload);

        var data = new byte[payload.Length + 1];
        payload.CopyTo(data, 0);
        data[payload.Length] = expectedCrc;

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 4 },
            new FieldDefinition
            {
                Name = "crc",
                Type = FieldType.UInt8,
                Checksum = new ChecksumSpec { Algorithm = "crc8-maxim", FieldNames = ["data"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var crcNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        crcNode.ChecksumValid.Should().BeTrue();
        crcNode.ChecksumAlgorithm.Should().Be("crc8-maxim");
    }

    [Fact]
    public void Decode_Crc8Cdma2000_ValidChecksum()
    {
        var payload = Encoding.ASCII.GetBytes("TEST");
        byte expectedCrc = Crc8Calculator.ComputeCdma2000(payload);

        var data = new byte[payload.Length + 1];
        payload.CopyTo(data, 0);
        data[payload.Length] = expectedCrc;

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 4 },
            new FieldDefinition
            {
                Name = "crc",
                Type = FieldType.UInt8,
                Checksum = new ChecksumSpec { Algorithm = "crc8-cdma2000", FieldNames = ["data"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var crcNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        crcNode.ChecksumValid.Should().BeTrue();
        crcNode.ChecksumAlgorithm.Should().Be("crc8-cdma2000");
    }

    [Fact]
    public void Decode_Crc64Ecma_ValidChecksum()
    {
        var payload = Encoding.ASCII.GetBytes("TESTDATA");
        var expectedCrc = Crc64Calculator.ComputeEcma(payload);
        var crcBytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(crcBytes, expectedCrc);

        var data = new byte[payload.Length + crcBytes.Length];
        payload.CopyTo(data, 0);
        crcBytes.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 8 },
            new FieldDefinition
            {
                Name = "crc",
                Type = FieldType.UInt64,
                Checksum = new ChecksumSpec { Algorithm = "crc64-ecma", FieldNames = ["data"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var crcNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        crcNode.ChecksumValid.Should().BeTrue();
        crcNode.ChecksumAlgorithm.Should().Be("crc64-ecma");
    }

    [Fact]
    public void Decode_XxHash32_ValidChecksum()
    {
        var payload = Encoding.ASCII.GetBytes("TEST");
        var expectedHash = XxHashCalculator.ComputeXxHash32(payload);
        var hashBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(hashBytes, expectedHash);

        var data = new byte[payload.Length + hashBytes.Length];
        payload.CopyTo(data, 0);
        hashBytes.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 4 },
            new FieldDefinition
            {
                Name = "hash",
                Type = FieldType.UInt32,
                Checksum = new ChecksumSpec { Algorithm = "xxhash32", FieldNames = ["data"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var hashNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        hashNode.ChecksumValid.Should().BeTrue();
        hashNode.ChecksumAlgorithm.Should().Be("xxhash32");
    }

    [Fact]
    public void Decode_XxHash64_ValidChecksum()
    {
        var payload = Encoding.ASCII.GetBytes("TESTDATA");
        var expectedHash = XxHashCalculator.ComputeXxHash64(payload);
        var hashBytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(hashBytes, expectedHash);

        var data = new byte[payload.Length + hashBytes.Length];
        payload.CopyTo(data, 0);
        hashBytes.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 8 },
            new FieldDefinition
            {
                Name = "hash",
                Type = FieldType.UInt64,
                Checksum = new ChecksumSpec { Algorithm = "xxhash64", FieldNames = ["data"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var hashNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        hashNode.ChecksumValid.Should().BeTrue();
        hashNode.ChecksumAlgorithm.Should().Be("xxhash64");
    }

    [Fact]
    public void Decode_Fletcher16_ValidChecksum()
    {
        var payload = Encoding.ASCII.GetBytes("TEST");
        var expectedChecksum = FletcherCalculator.ComputeFletcher16(payload);
        var checksumBytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(checksumBytes, expectedChecksum);

        var data = new byte[payload.Length + checksumBytes.Length];
        payload.CopyTo(data, 0);
        checksumBytes.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 4 },
            new FieldDefinition
            {
                Name = "checksum",
                Type = FieldType.UInt16,
                Checksum = new ChecksumSpec { Algorithm = "fletcher16", FieldNames = ["data"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var checksumNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        checksumNode.ChecksumValid.Should().BeTrue();
        checksumNode.ChecksumAlgorithm.Should().Be("fletcher16");
    }

    [Fact]
    public void Decode_Fletcher32_ValidChecksum()
    {
        var payload = Encoding.ASCII.GetBytes("TEST");
        var expectedChecksum = FletcherCalculator.ComputeFletcher32(payload);
        var checksumBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksumBytes, expectedChecksum);

        var data = new byte[payload.Length + checksumBytes.Length];
        payload.CopyTo(data, 0);
        checksumBytes.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 4 },
            new FieldDefinition
            {
                Name = "checksum",
                Type = FieldType.UInt32,
                Checksum = new ChecksumSpec { Algorithm = "fletcher32", FieldNames = ["data"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var checksumNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        checksumNode.ChecksumValid.Should().BeTrue();
        checksumNode.ChecksumAlgorithm.Should().Be("fletcher32");
    }

    [Fact]
    public void Decode_Sha384_ValidChecksum()
    {
        var payload = Encoding.ASCII.GetBytes("TESTDATA");
        var hash = SHA384.HashData(payload);

        var data = new byte[payload.Length + hash.Length];
        payload.CopyTo(data, 0);
        hash.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 8 },
            new FieldDefinition
            {
                Name = "hash",
                Type = FieldType.Bytes,
                Size = 48,
                Checksum = new ChecksumSpec { Algorithm = "sha384", FieldNames = ["data"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var hashNode = result.Children[1].Should().BeOfType<DecodedBytes>().Subject;
        hashNode.ChecksumValid.Should().BeTrue();
        hashNode.ChecksumAlgorithm.Should().Be("sha384");
    }

    [Fact]
    public void Decode_Sha512_ValidChecksum()
    {
        var payload = Encoding.ASCII.GetBytes("TESTDATA");
        var hash = SHA512.HashData(payload);

        var data = new byte[payload.Length + hash.Length];
        payload.CopyTo(data, 0);
        hash.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 8 },
            new FieldDefinition
            {
                Name = "hash",
                Type = FieldType.Bytes,
                Size = 64,
                Checksum = new ChecksumSpec { Algorithm = "sha512", FieldNames = ["data"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var hashNode = result.Children[1].Should().BeOfType<DecodedBytes>().Subject;
        hashNode.ChecksumValid.Should().BeTrue();
        hashNode.ChecksumAlgorithm.Should().Be("sha512");
    }

    [Fact]
    public void Decode_Sha512_InvalidChecksum_ReturnsExpectedHex()
    {
        var payload = Encoding.ASCII.GetBytes("TESTDATA");
        var wrongHash = new byte[64]; // all zeros

        var data = new byte[payload.Length + wrongHash.Length];
        payload.CopyTo(data, 0);
        wrongHash.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 8 },
            new FieldDefinition
            {
                Name = "hash",
                Type = FieldType.Bytes,
                Size = 64,
                Checksum = new ChecksumSpec { Algorithm = "sha512", FieldNames = ["data"] },
            });

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var hashNode = result.Children[1].Should().BeOfType<DecodedBytes>().Subject;
        hashNode.ChecksumValid.Should().BeFalse();
        hashNode.ChecksumExpectedHex.Should().NotBeNullOrEmpty();
        hashNode.ChecksumAlgorithm.Should().Be("sha512");
    }

    private static FormatDefinition MakeFormat(params FieldDefinition[] fields) => new()
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
                Fields = fields.ToList(),
            },
        },
        RootStruct = "root",
    };
}
