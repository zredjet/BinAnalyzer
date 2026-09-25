using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>REQ-201: 2^63 以上の符号なし 64 ビットの値。値は同じビットの long で持ち、表示は符号なし。式の中は符号付き。</summary>
public class Unsigned64DisplayTests
{
    private const string Yaml = """
        name: t
        endianness: big
        root: main
        enums:
          big:
            - value: 0xFFFFFFFFFFFFFFFF
              label: all_ones
            - value: 9223372036854775808
              label: high_bit
        structs:
          main:
            - name: x
              type: uint64
              enum: big
            - name: y
              type: uint64
              enum: big
            - name: bits
              type: bitfield
              size: "8"
              fields:
                - name: all
                  bits: "63:0"
                - name: top
                  bits: "63:60"
            - name: x_positive
              type: virtual
              value: "{x > 0 ? 1 : 0}"
            - name: v
              type: uleb128
        """;

    // x = 2^64 - 1、y = 2^63、bits = 0x8000000000000000、v = uleb128 の 2^64 - 1（10 バイト）
    private static readonly byte[] Data = Convert.FromHexString(
        "FFFFFFFFFFFFFFFF" + "8000000000000000" + "8000000000000000" + "FFFFFFFFFFFFFFFFFF01");

    private static DecodedStruct Decode() => new BinaryDecoder().Decode(Data, new YamlFormatLoader().LoadFromString(Yaml));

    private static T Child<T>(DecodedStruct s, string name) where T : DecodedNode => (T)s.Children.Single(c => c.Name == name);

    [Fact]
    public void Uint64_AboveInt64_IsShownUnsigned()
    {
        var root = Decode();

        var x = Child<DecodedInteger>(root, "x");
        x.Value.Should().Be(-1, "値は同じビットの long で持つ");
        x.ValueText.Should().Be("18446744073709551615");
        x.ShowsHex.Should().BeTrue();
        Child<DecodedInteger>(root, "y").ValueText.Should().Be("9223372036854775808");
        Child<DecodedInteger>(root, "v").ValueText.Should().Be("18446744073709551615");
    }

    [Fact]
    public void EnumValuesAbove2To63_MatchTheUint64Field()
    {
        var root = Decode();

        Child<DecodedInteger>(root, "x").EnumLabel.Should().Be("all_ones");
        Child<DecodedInteger>(root, "y").EnumLabel.Should().Be("high_bit");
    }

    [Fact]
    public void BitfieldEntryOf64Bits_KeepsAllBits()
    {
        // 以前は 64 ビットの幅のマスク（1L << 64）が 0 になり、値が 0 になっていた
        var bits = Child<DecodedBitfield>(Decode(), "bits");

        bits.Fields.Single(f => f.Name == "all").ValueText.Should().Be("9223372036854775808");
        bits.Fields.Single(f => f.Name == "top").Value.Should().Be(8, "上位ビットを符号拡張しない");
    }

    [Fact]
    public void Expressions_StaySigned()
    {
        // 式の整数は符号付き 64 ビット。2^64 - 1 は -1 として比べる（DSL リファレンスの「整数の値の範囲」）
        Child<DecodedVirtual>(Decode(), "x_positive").Value.Should().Be(0L);
    }

    [Fact]
    public void Edit_WritesValuesAbove2To63()
    {
        var x = Child<DecodedInteger>(Decode(), "x");

        FieldEncoder.Instance.InitialText(x).Should().Be("18446744073709551615");
        FieldEncoder.Instance.Encode(x, "9223372036854775808").Bytes.Should().Equal(Convert.FromHexString("8000000000000000"));
        FieldEncoder.Instance.Encode(x, "0xFFFFFFFFFFFFFFFE").Bytes.Should().Equal(Convert.FromHexString("FFFFFFFFFFFFFFFE"));
        FieldEncoder.Instance.Encode(x, "18446744073709551616").IsSuccess.Should().BeFalse("2^64 は範囲外");
        FieldEncoder.Instance.Encode(x, "-1").IsSuccess.Should().BeFalse("uint64 に負の値は書けない");
    }

    [Fact]
    public void Diff_ShowsUnsignedValues()
    {
        var left = Decode();
        var data = (byte[])Data.Clone();
        data[7] = 0xFE;
        var right = new BinaryDecoder().Decode(data, new YamlFormatLoader().LoadFromString(Yaml));

        var entry = DiffEngine.Compare(left, right).Entries.Single(e => e.FieldPath.EndsWith("x"));
        entry.OldValue.Should().StartWith("18446744073709551615");
        entry.NewValue.Should().StartWith("18446744073709551614");
    }
}
