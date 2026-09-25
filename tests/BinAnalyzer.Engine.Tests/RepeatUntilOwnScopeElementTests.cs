using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>
/// REQ-196: 独自の変数のスコープを持つ struct（scope: isolated・エンディアンの形・テンプレートの引数）を要素にした repeat_until で、
/// 要素の値を条件から引ける。
/// </summary>
public class RepeatUntilOwnScopeElementTests
{
    private readonly BinaryDecoder _decoder = new();

    // 値が 0 の要素（CBOR の break と同じ形）まで読み、後ろの 1 バイトを tail として読む
    private static string Yaml(string itemHeader, string structRef = "item") => $$"""
        name: t
        endianness: little
        root: main
        structs:
          main:
            - name: items
              type: struct
              struct: "{{structRef}}"
              repeat_until: "{is_last == 1}"
            - name: last_value
              type: virtual
              value: "{value}"
            - name: tail
              type: uint8
          {{itemHeader}}
            fields:
              - name: value
                type: uint8
              - name: is_last
                type: virtual
                value: "{value == 0 ? 1 : 0}"
        """;

    private static readonly byte[] Data = [3, 2, 0, 9];

    [Theory]
    [InlineData("scope: isolated")]
    [InlineData("endianness: little")]
    public void UntilCondition_SeesTheValuesOfAnOwnScopeElement(string scopeKey)
    {
        var result = Decode(Yaml($"item:\n    {scopeKey}"), Data);

        Values(result).Should().Equal(3L, 2L, 0L);
        Child<DecodedInteger>(result, "tail").Value.Should().Be(9);
        // 条件で引けた値は、繰り返しの後ろに昇格した値と同じ
        Child<DecodedVirtual>(result, "last_value").Value.Should().Be(0L);
    }

    [Fact]
    public void UntilCondition_SeesTheValuesOfATemplateElement()
    {
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: items
                  type: struct
                  struct: "item(stop=2)"
                  repeat_until: "{is_last == 1}"
                - name: tail
                  type: uint8
              item(stop):
                - name: value
                  type: uint8
                - name: is_last
                  type: virtual
                  value: "{value == stop ? 1 : 0}"
            """;

        var result = Decode(yaml, [3, 2, 7]);

        Values(result).Should().Equal(3L, 2L);
        Child<DecodedInteger>(result, "tail").Value.Should().Be(7);
    }

    [Fact]
    public void UntilCondition_WithElementSize_SeesTheValuesOfAnIsolatedElement()
    {
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: items
                  type: struct
                  struct: item
                  element_size: "2"
                  repeat_until: "{is_last == 1}"
                - name: tail
                  type: uint8
              item:
                scope: isolated
                fields:
                  - name: value
                    type: uint8
                  - name: is_last
                    type: virtual
                    value: "{value == 0 ? 1 : 0}"
            """;

        var result = Decode(yaml, [3, 0xAA, 0, 0xBB, 9]);

        Values(result).Should().Equal(3L, 0L);
        Child<DecodedInteger>(result, "tail").Value.Should().Be(9);
    }

    [Fact]
    public void UntilCondition_OfAPlainElement_StillUsesTheElementsOwnValue()
    {
        // 大きさの無い struct の要素は従来どおり: 条件は要素の value を見る（中のエンディアンの形の struct の value ではない）
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: items
                  type: struct
                  struct: item
                  repeat_until: "{value == 0}"
                - name: tail
                  type: uint8
              item:
                - name: value
                  type: uint8
                - name: inner
                  type: struct
                  struct: inner
              inner:
                endianness: little
                fields:
                  - name: value
                    type: uint8
            """;

        // 要素 1 = (5, inner 0)、要素 2 = (0, inner 7)
        var result = Decode(yaml, [5, 0, 0, 7, 9]);

        Values(result).Should().Equal(5L, 0L);
        Child<DecodedInteger>(result, "tail").Value.Should().Be(9);
    }

    private DecodedStruct Decode(string yaml, byte[] data) =>
        _decoder.Decode(data, new YamlFormatLoader().LoadFromString(yaml));

    private static IEnumerable<long> Values(DecodedStruct root) =>
        Child<DecodedArray>(root, "items").Elements.Cast<DecodedStruct>().Select(e => Child<DecodedInteger>(e, "value").Value);

    private static T Child<T>(DecodedStruct parent, string name) where T : DecodedNode =>
        parent.Children.Single(c => c.Name == name).Should().BeOfType<T>().Subject;
}
