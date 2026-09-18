using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>REQ-171: デコード結果ノードが DSL の型名を保持する。</summary>
public class TypeNameTests
{
    private const string Yaml = """
        name: typed
        endianness: big
        root: root
        enums:
          kind:
            - value: 1
              label: one
        structs:
          root:
            - name: u
              type: uint32
            - name: i
              type: int16
            - name: f
              type: float32
            - name: s
              type: ascii
              size: "4"
            - name: b
              type: bytes
              size: "2"
            - name: v
              type: uleb128
            - name: k
              type: u8
              enum: kind
            - name: z
              type: asciiz
            - name: bf
              type: bitfield
              size: "1"
              fields:
                - name: hi
                  bits: "7:4"
                - name: lo
                  bits: "3:0"
            - name: inner
              type: struct
              struct: inner
            - name: sw
              type: switch
              switch_on: "{k}"
              cases:
                "1": inner
              default: inner
            - name: arr
              type: uint8
              repeat_count: "2"
            - name: calc
              type: virtual
              value: "{u + 1}"
          inner:
            - name: x
              type: uint8
        """;

    private static DecodedStruct Decode()
    {
        var format = new YamlFormatLoader().LoadFromString(Yaml);
        var data = new byte[] { 0, 0, 0, 5, 0xFF, 0xFE, 0x3F, 0x80, 0, 0, (byte)'I', (byte)'H', (byte)'D', (byte)'R', 1, 2, 0x81, 0x01, 1, (byte)'a', 0, 0x12, 7, 8, 9, 10 };
        return new BinaryDecoder().Decode(data, format);
    }

    private static DecodedNode Child(DecodedStruct root, string name) => root.Children.First(c => c.Name == name);

    [Theory]
    [InlineData("u", "uint32")]
    [InlineData("i", "int16")]
    [InlineData("f", "float32")]
    [InlineData("s", "ascii")]
    [InlineData("b", "bytes")]
    [InlineData("v", "uleb128")]
    [InlineData("k", "uint8")]
    [InlineData("z", "asciiz")]
    [InlineData("bf", "bitfield")]
    [InlineData("inner", "struct")]
    [InlineData("sw", "switch")]
    [InlineData("arr", "uint8")]
    [InlineData("calc", "virtual")]
    public void Decode_SetsTypeName_ForEveryNodeKind(string field, string expected)
    {
        var root = Decode();
        var node = Child(root, field);
        node.TypeName.Should().Be(expected);
    }

    [Fact]
    public void Decode_AliasTypeName_IsCanonical_AndRootHasNone()
    {
        var root = Decode();
        root.TypeName.Should().BeNull("ルートは DSL のフィールドではない");
        ((DecodedInteger)Child(root, "k")).DslType.Should().Be(FieldType.UInt8, "u8 は uint8 の別名");
        ((DecodedInteger)Child(root, "k")).EnumRef.Should().Be("kind");
        var arr = (DecodedArray)Child(root, "arr");
        arr.Elements.Should().AllSatisfy(e => e.TypeName.Should().Be("uint8"));
        ((DecodedStruct)Child(root, "inner")).Children[0].TypeName.Should().Be("uint8");
    }

    [Fact]
    public void Decode_CompressedNode_KeepsCompressionTypeName()
    {
        var format = new FormatDefinition
        {
            Name = "z",
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["root"] = new() { Name = "root", Fields = [new FieldDefinition { Name = "payload", Type = FieldType.Zlib, SizeRemaining = true }] },
            },
            RootStruct = "root",
        };
        using var ms = new MemoryStream();
        using (var z = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
            z.Write(new byte[] { 1, 2, 3 });
        var root = new BinaryDecoder().Decode(ms.ToArray(), format);
        root.Children[0].Should().BeOfType<DecodedCompressed>().Which.TypeName.Should().Be("zlib");
    }

    [Fact]
    public void FieldTypeNames_RoundTrip_AllTypes()
    {
        foreach (var type in Enum.GetValues<FieldType>())
        {
            var name = FieldTypeNames.ToDslName(type);
            FieldTypeNames.TryParse(name, out var parsed).Should().BeTrue(name);
            parsed.Should().Be(type);
            FieldTypeNames.ShortLabel(type).Should().NotBeNullOrEmpty();
        }
        FieldTypeNames.TryParse("U32", out var alias).Should().BeTrue();
        alias.Should().Be(FieldType.UInt32);
        FieldTypeNames.TryParse("nope", out _).Should().BeFalse();
        FieldTypeNames.ShortLabel(FieldType.Int16).Should().Be("i16");
        FieldTypeNames.ShortLabel(FieldType.Float64).Should().Be("f64");
        FieldTypeNames.ShortLabel(FieldType.Ascii).Should().Be("ascii");
    }
}
