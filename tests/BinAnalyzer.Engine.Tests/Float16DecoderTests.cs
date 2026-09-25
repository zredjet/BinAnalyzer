using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Patching;
using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>REQ-200: 型 <c>float16</c>（IEEE 754 binary16）。</summary>
public class Float16DecoderTests
{
    public static TheoryData<string, double> Values => new()
    {
        { "3C00", 1.0 },
        { "C000", -2.0 },
        { "7BFF", 65504.0 },                 // 最大の有限の値
        { "0001", 5.960464477539063e-8 },    // 最小の非正規化数
        { "0400", 0.00006103515625 },        // 最小の正規化数
        { "3555", 0.333251953125 },
        { "7C00", double.PositiveInfinity },
        { "FC00", double.NegativeInfinity },
        { "7E00", double.NaN },
    };

    [Theory]
    [MemberData(nameof(Values))]
    public void Decode_BigAndLittleEndian(string bigEndianHex, double expected)
    {
        var big = Convert.FromHexString(bigEndianHex);

        Decode("big", "float16", big).Value.Should().Be(expected);
        Decode("little", "float16", [big[1], big[0]]).Value.Should().Be(expected);
    }

    [Fact]
    public void Decode_NegativeZero_KeepsTheSign()
    {
        double.IsNegative(Decode("big", "float16", [0x80, 0x00]).Value).Should().BeTrue();
    }

    [Fact]
    public void Decode_ProducesAHalfPrecisionNodeOf2Bytes()
    {
        var value = Decode("big", "f16", [0x3C, 0x00]);   // 別名

        value.Precision.Should().Be(FloatPrecision.Half);
        value.FloatTypeName.Should().Be("float16");
        value.TypeName.Should().Be("float16");
        value.Size.Should().Be(2);
        value.Endianness.Should().Be(Endianness.Big);
    }

    [Fact]
    public void Decode_ValueIsBoundAsAVariable()
    {
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: scale
                  type: float16
                - name: copy
                  type: virtual
                  value: "{scale}"
                - name: tail
                  type: uint8
            """;

        var root = new BinaryDecoder().Decode(new byte[] { 0x00, 0x3E, 0x07 }, new YamlFormatLoader().LoadFromString(yaml));   // 1.5

        root.Children.OfType<DecodedVirtual>().Single().Value.Should().Be(1.5);   // float32 / float64 と同じく double の変数になる
        root.Children.OfType<DecodedInteger>().Single().Value.Should().Be(7);
    }

    [Fact]
    public void Decode_TooShort_IsAnError()
    {
        var act = () => Decode("big", "float16", [0x3C]);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Edit_IsAFloatEdit()
    {
        FieldEditRules.Classify(Decode("big", "float16", [0x3C, 0x00]), out _).Should().Be(EditKind.Float);
    }

    [Fact]
    public void Encode_BigAndLittle_AndRejectsOutOfRange()
    {
        var big = Decode("big", "float16", [0, 0]);
        var little = Decode("little", "float16", [0, 0]);

        FieldEncoder.Instance.Encode(big, "1.5").Bytes.Should().Equal(new byte[] { 0x3E, 0x00 });
        FieldEncoder.Instance.Encode(little, "1.5").Bytes.Should().Equal(new byte[] { 0x00, 0x3E });
        FieldEncoder.Instance.Encode(big, "65504").Bytes.Should().Equal(new byte[] { 0x7B, 0xFF });
        FieldEncoder.Instance.Encode(big, "-Infinity").Bytes.Should().Equal(new byte[] { 0xFC, 0x00 });
        var overflow = FieldEncoder.Instance.Encode(big, "70000");
        overflow.IsSuccess.Should().BeFalse();
        overflow.Error.Should().Contain("float16");
    }

    private static DecodedFloat Decode(string endianness, string type, byte[] data)
    {
        var yaml = $"""
            name: t
            endianness: {endianness}
            root: main
            structs:
              main:
                - name: value
                  type: {type}
            """;
        return (DecodedFloat)new BinaryDecoder().Decode(data, new YamlFormatLoader().LoadFromString(yaml)).Children.Single();
    }
}
