using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>REQ-197: 0 バイトで成功する要素が続く繰り返しを、連続した数で打ち切る。</summary>
public class ZeroLengthRepeatGuardTests
{
    private readonly BinaryDecoder _decoder = new();

    // 0 バイトの要素（virtual だけの struct）を repeat で並べる
    private static string Yaml(string repeat) => $$"""
        name: t
        endianness: little
        root: main
        structs:
          main:
            - name: items
              type: struct
              struct: empty
              {{repeat}}
            - name: tail
              type: bytes
              size: remaining
          empty:
            - name: index
              type: virtual
              value: "{_index}"
        """;

    [Fact]
    public void ZeroLengthElements_WithAHugeCount_AreTruncatedAtTheDefaultLimit()
    {
        var items = Items(Decode(Yaml("repeat_count: \"{2000000000}\""), [1, 2]));

        items.Elements.Should().HaveCount(BinaryDecoder.DefaultMaxZeroLengthElements);
        items.Truncated.Should().BeTrue();
        items.TruncationReason.Should().Contain("consumed 0 bytes");
    }

    [Theory]
    [InlineData("repeat: eof")]
    [InlineData("repeat_until: \"{index < 0}\"")]
    [InlineData("repeat_while: \"{1}\"")]
    public void ZeroLengthElements_InOtherRepeatModes_AreTruncated(string repeat)
    {
        // 今まではこれらのモードも終わらなかった
        var items = Items(Decode(Yaml(repeat), [1, 2], maxZeroLength: 100));

        items.Elements.Should().HaveCount(100);
        items.Truncated.Should().BeTrue();
    }

    [Fact]
    public void ZeroLengthElements_BelowTheLimit_AreNotTruncated()
    {
        var items = Items(Decode(Yaml("repeat_count: \"{1000}\""), [1, 2]));

        items.Elements.Should().HaveCount(1000);
        items.Truncated.Should().BeFalse();
    }

    [Fact]
    public void AlternatingZeroLengthAndReadingElements_AreNotTruncated()
    {
        // 奇数番の要素だけ 1 バイト読む。0 バイトの要素は続かないので、上限 2 でも打ち切らない
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: items
                  type: struct
                  struct: item
                  repeat_count: "10"
              item:
                - name: value
                  type: uint8
                  if: "{_index % 2 == 1}"
            """;

        var items = Items(Decode(yaml, [1, 2, 3, 4, 5], maxZeroLength: 2));

        items.Elements.Should().HaveCount(10);
        items.Truncated.Should().BeFalse();
    }

    [Fact]
    public void PerElementSeek_IsNotCounted()
    {
        // 要素ごとの seek + seek_restore は位置が進まないが、対象外
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: items
                  type: uint8
                  repeat_count: "10"
                  seek: "{_index % 2}"
                  seek_restore: true
            """;

        var items = Items(Decode(yaml, [7, 8], maxZeroLength: 2));

        items.Elements.Should().HaveCount(10);
        items.Truncated.Should().BeFalse();
    }

    private DecodedStruct Decode(string yaml, byte[] data, int? maxZeroLength = null) =>
        _decoder.Decode(data, new YamlFormatLoader().LoadFromString(yaml), new DecodeOptions { MaxZeroLengthElements = maxZeroLength });

    private static DecodedArray Items(DecodedStruct root) =>
        root.Children.Single(c => c.Name == "items").Should().BeOfType<DecodedArray>().Subject;
}
