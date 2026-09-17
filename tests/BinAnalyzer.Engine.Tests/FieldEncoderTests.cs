using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Patching;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>REQ-169: 値 → バイト列のエンコード（範囲チェック・エンディアン・固定長）。</summary>
public class FieldEncoderTests
{
    private static DecodedInteger Int(FieldType type, long size, Endianness endian = Endianness.Big, long value = 0, string? enumRef = null)
        => new() { Name = "f", Offset = 0, Size = size, Value = value, DslType = type, Endianness = endian, EnumRef = enumRef };

    [Theory]
    [InlineData(FieldType.UInt8, 1, Endianness.Big, "255", new byte[] { 0xFF })]
    [InlineData(FieldType.UInt16, 2, Endianness.Big, "0x1234", new byte[] { 0x12, 0x34 })]
    [InlineData(FieldType.UInt16, 2, Endianness.Little, "0x1234", new byte[] { 0x34, 0x12 })]
    [InlineData(FieldType.UInt32, 4, Endianness.Big, "2", new byte[] { 0, 0, 0, 2 })]
    [InlineData(FieldType.UInt32, 4, Endianness.Little, "2", new byte[] { 2, 0, 0, 0 })]
    [InlineData(FieldType.Int8, 1, Endianness.Big, "-1", new byte[] { 0xFF })]
    [InlineData(FieldType.Int16, 2, Endianness.Little, "-2", new byte[] { 0xFE, 0xFF })]
    [InlineData(FieldType.Int32, 4, Endianness.Big, "-0x10", new byte[] { 0xFF, 0xFF, 0xFF, 0xF0 })]
    [InlineData(FieldType.UInt64, 8, Endianness.Big, "18446744073709551615", new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
    [InlineData(FieldType.Int64, 8, Endianness.Little, "-9223372036854775808", new byte[] { 0, 0, 0, 0, 0, 0, 0, 0x80 })]
    public void Encode_Integer_RespectsSizeSignAndEndianness(FieldType type, long size, Endianness endian, string input, byte[] expected)
    {
        var result = FieldEncoder.Instance.Encode(Int(type, size, endian), input);
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Bytes.Should().Equal(expected);
    }

    [Theory]
    [InlineData(FieldType.UInt8, 1, "300")]
    [InlineData(FieldType.UInt8, 1, "-1")]
    [InlineData(FieldType.Int8, 1, "128")]
    [InlineData(FieldType.Int8, 1, "-129")]
    [InlineData(FieldType.UInt16, 2, "65536")]
    [InlineData(FieldType.UInt32, 4, "4294967296")]
    [InlineData(FieldType.Int32, 4, "2147483648")]
    [InlineData(FieldType.UInt64, 8, "18446744073709551616")]
    public void Encode_Integer_OutOfRange_Fails(FieldType type, long size, string input)
    {
        var result = FieldEncoder.Instance.Encode(Int(type, size), input);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("範囲外");
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("0x")]
    [InlineData("0xZZ")]
    public void Encode_Integer_Unparseable_Fails(string input)
    {
        var result = FieldEncoder.Instance.Encode(Int(FieldType.UInt32, 4), input);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Encode_Integer_TrimsWhitespace_AndAcceptsPlusSign()
    {
        FieldEncoder.Instance.Encode(Int(FieldType.UInt8, 1), "  +7 ").Bytes.Should().Equal(new byte[] { 7 });
    }

    [Fact]
    public void Encode_Float32_BigAndLittle()
    {
        var big = new DecodedFloat { Name = "f", Offset = 0, Size = 4, Value = 0, IsSinglePrecision = true, DslType = FieldType.Float32, Endianness = Endianness.Big };
        var little = new DecodedFloat { Name = "f", Offset = 0, Size = 4, Value = 0, IsSinglePrecision = true, DslType = FieldType.Float32, Endianness = Endianness.Little };
        FieldEncoder.Instance.Encode(big, "1.5").Bytes.Should().Equal(new byte[] { 0x3F, 0xC0, 0, 0 });
        FieldEncoder.Instance.Encode(little, "1.5").Bytes.Should().Equal(new byte[] { 0, 0, 0xC0, 0x3F });
        FieldEncoder.Instance.Encode(big, "1e40").IsSuccess.Should().BeFalse("float32 の範囲外");
        FieldEncoder.Instance.Encode(big, "x").IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Encode_Float64_LittleEndian()
    {
        var node = new DecodedFloat { Name = "f", Offset = 0, Size = 8, Value = 0, IsSinglePrecision = false, DslType = FieldType.Float64, Endianness = Endianness.Little };
        FieldEncoder.Instance.Encode(node, "1.0").Bytes.Should().Equal(BitConverter.GetBytes(1.0));
    }

    [Fact]
    public void Encode_FixedString_PadsWithNul_AndRejectsOverflow()
    {
        var node = new DecodedString { Name = "type", Offset = 0, Size = 4, Value = "IHDR", Encoding = "ascii", DslType = FieldType.Ascii };
        var exact = FieldEncoder.Instance.Encode(node, "IEND");
        exact.Bytes.Should().Equal("IEND"u8.ToArray());
        exact.Note.Should().BeNull();

        var shorter = FieldEncoder.Instance.Encode(node, "AB");
        shorter.Bytes.Should().Equal(new byte[] { 0x41, 0x42, 0, 0 });
        shorter.Note.Should().Contain("0x00");

        FieldEncoder.Instance.Encode(node, "TOOLONG").Error.Should().Contain("超えています");
        FieldEncoder.Instance.Encode(node, "日本").Error.Should().Contain("ASCII");
    }

    [Fact]
    public void Encode_Utf16String_UsesEncodingByteLength()
    {
        var node = new DecodedString { Name = "s", Offset = 0, Size = 4, Value = "ab", Encoding = "utf16le", DslType = FieldType.Utf16Le };
        FieldEncoder.Instance.Encode(node, "ab").Bytes.Should().Equal(new byte[] { 0x61, 0, 0x62, 0 });
        FieldEncoder.Instance.Encode(node, "abc").IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Encode_Bytes_ParsesHexWithSeparators_AndRequiresExactLength()
    {
        var node = new DecodedBytes { Name = "sig", Offset = 0, Size = 4, RawBytes = new byte[4], DslType = FieldType.Bytes };
        FieldEncoder.Instance.Encode(node, "89 50 4e 47").Bytes.Should().Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        FieldEncoder.Instance.Encode(node, "0x89,0x50:0x4E-0x47").Bytes.Should().Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        FieldEncoder.Instance.Encode(node, "89504E").Error.Should().Contain("4 バイト必要");
        FieldEncoder.Instance.Encode(node, "89504E4").Error.Should().Contain("奇数");
        FieldEncoder.Instance.Encode(node, "89 50 4E GG").Error.Should().Contain("16 進");
    }

    [Fact]
    public void CanEncode_RejectsVariableLength_Bitstream_AndMissingMetadata()
    {
        FieldEncoder.Instance.CanEncode(Int(FieldType.ULeb128, 3), out var r1).Should().BeFalse();
        r1.Should().Contain("可変長");

        var bits = new DecodedInteger { Name = "b", Offset = 0, Size = 3, BitOffset = 2, Value = 1, DslType = FieldType.UInt8 };
        FieldEncoder.Instance.CanEncode(bits, out var r2).Should().BeFalse();
        r2.Should().Contain("ビットストリーム");

        var noType = new DecodedInteger { Name = "n", Offset = 0, Size = 4, Value = 1 };
        FieldEncoder.Instance.CanEncode(noType, out _).Should().BeFalse();

        var z = new DecodedString { Name = "z", Offset = 0, Size = 5, Value = "abcd", Encoding = "asciiz", DslType = FieldType.AsciiZ };
        FieldEncoder.Instance.CanEncode(z, out var r3).Should().BeFalse();
        r3.Should().Contain("NUL");

        var st = new DecodedStruct { Name = "s", Offset = 0, Size = 4, StructType = "t", Children = [] };
        FieldEncoder.Instance.CanEncode(st, out _).Should().BeFalse();
        FieldEncoder.Instance.Encode(st, "1").IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void InitialText_ReflectsCurrentValue()
    {
        FieldEncoder.Instance.InitialText(Int(FieldType.Int16, 2, value: -5)).Should().Be("-5");
        FieldEncoder.Instance.InitialText(Int(FieldType.UInt64, 8, value: -1)).Should().Be("18446744073709551615");
        FieldEncoder.Instance.InitialText(new DecodedFloat { Name = "f", Offset = 0, Size = 4, Value = 0.5, IsSinglePrecision = true }).Should().Be("0.5");
        FieldEncoder.Instance.InitialText(new DecodedString { Name = "s", Offset = 0, Size = 4, Value = "AB\0\0", Encoding = "ascii" }).Should().Be("AB");
        FieldEncoder.Instance.InitialText(new DecodedBytes { Name = "b", Offset = 0, Size = 2, RawBytes = new byte[] { 0xAB, 0x01 } }).Should().Be("AB 01");
    }

    [Fact]
    public void EditRules_ClassifiesEnumAsEnum()
    {
        FieldEditRules.Classify(Int(FieldType.UInt8, 1, enumRef: "color"), out _).Should().Be(EditKind.Enum);
        FieldEditRules.Classify(Int(FieldType.UInt8, 1), out _).Should().Be(EditKind.Integer);
    }
}
