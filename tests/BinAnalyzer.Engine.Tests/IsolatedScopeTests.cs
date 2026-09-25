using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>
/// REQ-195: <c>scope: isolated</c> の struct は独自の変数のスコープを持ち、中の値は親に書かれない。
/// 繰り返しの要素の値の昇格も、入れ子の isolated の struct の中へは入らない。
/// </summary>
public class IsolatedScopeTests
{
    private readonly BinaryDecoder _decoder = new();

    // 自分を入れ子にする struct（DNS の名前と同じ形）: tag が 1 より大きければ次のバイトを子として読み、最後に自分の tag を使う
    private static string RecursiveYaml(string scope) => $$"""
        name: t
        endianness: little
        root: main
        structs:
          main:
            - name: top
              type: struct
              struct: node
          node:
            {{scope}}
            fields:
              - name: tag
                type: uint8
              - name: child
                type: struct
                struct: node
                if: "{tag > 1}"
              - name: own_tag
                type: virtual
                value: "{tag}"
        """;

    [Fact]
    public void Isolated_RecursiveStruct_KeepsItsOwnValues()
    {
        var top = Child<DecodedStruct>(Decode(RecursiveYaml("scope: isolated"), [3, 2, 1]), "top");

        Child<DecodedVirtual>(top, "own_tag").Value.Should().Be(3L);
        var child = Child<DecodedStruct>(top, "child");
        Child<DecodedVirtual>(child, "own_tag").Value.Should().Be(2L);
        Child<DecodedVirtual>(Child<DecodedStruct>(child, "child"), "own_tag").Value.Should().Be(1L);
    }

    [Fact]
    public void NotIsolated_RecursiveStruct_IsOverwrittenByTheChild()
    {
        // 指定しなければ従来どおり親とスコープを共有し、子の tag が親の tag を上書きする
        var top = Child<DecodedStruct>(Decode(RecursiveYaml(""), [3, 2, 1]), "top");

        Child<DecodedVirtual>(top, "own_tag").Value.Should().Be(1L);
    }

    [Fact]
    public void Isolated_ReadsOuterVariables_AndMemberAccessReachesItsValues()
    {
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: base
                  type: uint8
                - name: inner
                  type: struct
                  struct: inner
                - name: from_outside
                  type: virtual
                  value: "{inner.value}"
              inner:
                scope: isolated
                fields:
                  - name: value
                    type: uint8
                  - name: sum
                    type: virtual
                    value: "{base + value}"
            """;

        var result = Decode(yaml, [10, 5]);

        Child<DecodedVirtual>(Child<DecodedStruct>(result, "inner"), "sum").Value.Should().Be(15L);
        Child<DecodedVirtual>(result, "from_outside").Value.Should().Be(5L);
        result.Children.Should().NotContain(c => c.Name == "value", "inner の値は親のスコープに書かれない（フィールドとしても並ばない）");
    }

    [Fact]
    public void Isolated_ValueIsNotBoundInTheParentScope()
    {
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: value
                  type: uint8
                - name: inner
                  type: struct
                  struct: inner
                - name: seen
                  type: virtual
                  value: "{value}"
              inner:
                scope: isolated
                fields:
                  - name: value
                    type: uint8
            """;

        Child<DecodedVirtual>(Decode(yaml, [1, 2]), "seen").Value.Should().Be(1L);
    }

    [Fact]
    public void Promotion_DoesNotEnterNestedIsolatedStructs()
    {
        // 要素（isolated でない）の直下の a は昇格し、要素の中の isolated の struct の v は昇格しない
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: v
                  type: virtual
                  value: "{0}"
                - name: items
                  type: struct
                  struct: item
                  repeat_count: "2"
                - name: last_a
                  type: virtual
                  value: "{a}"
                - name: seen_v
                  type: virtual
                  value: "{v}"
              item:
                - name: a
                  type: uint8
                - name: inner
                  type: struct
                  struct: inner
              inner:
                scope: isolated
                fields:
                  - name: v
                    type: uint8
            """;

        var result = Decode(yaml, [1, 10, 2, 20]);

        Child<DecodedVirtual>(result, "last_a").Value.Should().Be(2L);
        Child<DecodedVirtual>(result, "seen_v").Value.Should().Be(0L);
    }

    [Fact]
    public void Promotion_OfAnIsolatedElement_KeepsItsDirectValues()
    {
        // 要素そのものが isolated なら、直下の値は昇格する（中の isolated の struct には入らない）
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: v
                  type: virtual
                  value: "{0}"
                - name: items
                  type: struct
                  struct: item
                  repeat_count: "2"
                - name: last_w
                  type: virtual
                  value: "{w}"
                - name: seen_v
                  type: virtual
                  value: "{v}"
              item:
                scope: isolated
                fields:
                  - name: w
                    type: uint8
                  - name: inner
                    type: struct
                    struct: inner
              inner:
                scope: isolated
                fields:
                  - name: v
                    type: uint8
            """;

        var result = Decode(yaml, [1, 10, 2, 20]);

        Child<DecodedVirtual>(result, "last_w").Value.Should().Be(2L);
        Child<DecodedVirtual>(result, "seen_v").Value.Should().Be(0L);
    }

    [Fact]
    public void Isolated_SwitchCase_IsNotEnteredByPromotion()
    {
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: v
                  type: virtual
                  value: "{0}"
                - name: items
                  type: struct
                  struct: item
                  repeat_count: "1"
                - name: seen_v
                  type: virtual
                  value: "{v}"
              item:
                - name: kind
                  type: uint8
                - name: body
                  type: switch
                  switch_on: "{kind}"
                  cases:
                    "1": inner
                  default: inner
              inner:
                scope: isolated
                fields:
                  - name: v
                    type: uint8
            """;

        Child<DecodedVirtual>(Decode(yaml, [1, 7]), "seen_v").Value.Should().Be(0L);
    }

    private DecodedStruct Decode(string yaml, byte[] data) =>
        _decoder.Decode(data, new YamlFormatLoader().LoadFromString(yaml));

    private static T Child<T>(DecodedStruct parent, string name) where T : DecodedNode =>
        parent.Children.Single(c => c.Name == name).Should().BeOfType<T>().Subject;
}
