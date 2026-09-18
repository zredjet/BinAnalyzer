using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>
/// REQ-180: struct / array の変数は辞書のコピーではなくデコード結果ノードそのものを束縛し、式評価時に子を引く。
/// 以前の辞書ベースと同じ結果になること（同名は後勝ち、bitfield のサブフィールドもメンバー、配列要素のメンバー、len / sum）。
/// </summary>
public class NodeValuesTests
{
    private static DecodedStruct Decode(string yaml, byte[] data)
        => new BinaryDecoder().Decode(data, new YamlFormatLoader().LoadFromString(yaml));

    private static object VirtualValue(DecodedStruct root, string name)
        => ((DecodedVirtual)root.Children.First(c => c.Name == name)).Value;

    [Fact]
    public void NestedStructMember_AndBitfieldSubField_AreAccessible()
    {
        var root = Decode("""
            name: T
            endianness: big
            root: main
            structs:
              main:
                - name: hdr
                  type: struct
                  struct: hdr
                - name: w
                  type: virtual
                  value: "{hdr.dims.width}"
                - name: f
                  type: virtual
                  value: "{hdr.compressed}"
              hdr:
                - name: dims
                  type: struct
                  struct: dims
                - name: flags
                  type: bitfield
                  size: "1"
                  fields:
                    - name: compressed
                      bits: "7"
                    - name: level
                      bits: "3:0"
              dims:
                - name: width
                  type: uint16
                - name: height
                  type: uint16
            """, [0x00, 0x10, 0x00, 0x20, 0x85]);

        VirtualValue(root, "w").Should().Be(16L);
        VirtualValue(root, "f").Should().Be(1L);
    }

    [Fact]
    public void DuplicateChildName_LastOneWins_LikeTheOldDictionary()
    {
        var root = Decode("""
            name: T
            endianness: big
            root: main
            structs:
              main:
                - name: s
                  type: struct
                  struct: s
                - name: picked
                  type: virtual
                  value: "{s.v}"
              s:
                - name: v
                  type: uint8
                - name: v
                  type: uint8
            """, [1, 2]);

        VirtualValue(root, "picked").Should().Be(2L);
    }

    [Fact]
    public void StructArray_ElementMember_Len_And_Sum_WorkThroughTheArrayNode()
    {
        var root = Decode("""
            name: T
            endianness: big
            root: main
            structs:
              main:
                - name: n
                  type: uint8
                - name: items
                  type: struct
                  struct: item
                  repeat_count: "{n}"
                - name: second
                  type: virtual
                  value: "{items[1].v}"
                - name: count
                  type: virtual
                  value: "{len(items)}"
                - name: vals
                  type: uint8
                  repeat_count: "{n}"
                - name: total
                  type: virtual
                  value: "{sum(vals)}"
                - name: biggest
                  type: virtual
                  value: "{max(vals)}"
              item:
                - name: v
                  type: uint8
            """, [3, 10, 20, 30, 1, 2, 3]);

        VirtualValue(root, "second").Should().Be(20L);
        VirtualValue(root, "count").Should().Be(3L);
        VirtualValue(root, "total").Should().Be(6L);
        VirtualValue(root, "biggest").Should().Be(3L);
    }

    [Fact]
    public void ArrayWithNonValueElements_IsNotBoundAsAVariable()
    {
        // bytes 要素の配列は式から参照できない（以前も辞書に入らなかった）
        var act = () => Decode("""
            name: T
            endianness: big
            root: main
            structs:
              main:
                - name: blobs
                  type: bytes
                  size: "1"
                  repeat_count: "2"
                - name: n
                  type: virtual
                  value: "{len(blobs)}"
            """, [1, 2]);
        act.Should().Throw<Core.DecodeException>().WithMessage("*blobs*");
    }

    [Fact]
    public void EnumFindByValue_ReturnsFirstEntry_ForDuplicateValues()
    {
        var def = new EnumDefinition
        {
            Name = "e",
            Entries = [new EnumEntry(1, "first"), new EnumEntry(1, "second"), new EnumEntry(2, "two")],
        };
        def.FindByValue(1)!.Label.Should().Be("first");
        def.FindByValue(2)!.Label.Should().Be("two");
        def.FindByValue(3).Should().BeNull();
    }

    [Fact]
    public void DecodedInteger_OptionalProperties_RoundTrip_AndDefaultToNull()
    {
        var plain = new DecodedInteger { Name = "a", Offset = 0, Size = 1, Value = 1, EnumLabel = null, ChecksumValid = null };
        plain.EnumLabel.Should().BeNull();
        plain.ChecksumValid.Should().BeNull();
        plain.ChecksumCoverage.Should().BeNull();

        var rich = new DecodedInteger
        {
            Name = "b", Offset = 0, Size = 4, Value = 7, EnumLabel = "seven", EnumRef = "digits",
            ChecksumValid = true, ChecksumExpected = 7, ChecksumAlgorithm = "crc32", StringTableValue = "s",
            Endianness = Endianness.Little,
        };
        rich.EnumLabel.Should().Be("seven");
        rich.EnumRef.Should().Be("digits");
        rich.ChecksumValid.Should().BeTrue();
        rich.ChecksumExpected.Should().Be(7);
        rich.ChecksumAlgorithm.Should().Be("crc32");
        rich.StringTableValue.Should().Be("s");
        rich.Endianness.Should().Be(Endianness.Little);
    }
}
