using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class StructMemberAccessTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void StructField_RegisteredAsDictionary()
    {
        // struct型フィールドのデコード後に {struct.member} で参照可能
        var format = new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["main"] = new()
                {
                    Name = "main",
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "header",
                            Type = FieldType.Struct,
                            StructRef = "header_struct",
                        },
                        new FieldDefinition
                        {
                            Name = "payload",
                            Type = FieldType.Bytes,
                            SizeExpression = ExpressionParser.Parse("{header.length}"),
                        },
                    ],
                },
                ["header_struct"] = new()
                {
                    Name = "header_struct",
                    Fields =
                    [
                        new FieldDefinition { Name = "magic", Type = FieldType.UInt8 },
                        new FieldDefinition { Name = "length", Type = FieldType.UInt8 },
                    ],
                },
            },
            RootStruct = "main",
        };

        // data: [magic=0xAB, length=3, payload=0x01, 0x02, 0x03]
        var data = new byte[] { 0xAB, 0x03, 0x01, 0x02, 0x03 };
        var result = _decoder.Decode(data, format);

        var payload = result.Children[1].Should().BeOfType<DecodedBytes>().Subject;
        payload.Size.Should().Be(3);
    }

    [Fact]
    public void StructArray_ElementsAccessibleByMember()
    {
        // {array[i].field} で構造体配列要素のフィールドを参照
        var format = new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["main"] = new()
                {
                    Name = "main",
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "entries",
                            Type = FieldType.Struct,
                            StructRef = "entry",
                            Repeat = new RepeatMode.Count(ExpressionParser.Parse("{2}")),
                        },
                        new FieldDefinition
                        {
                            Name = "first_offset",
                            Type = FieldType.Virtual,
                            ValueExpression = ExpressionParser.Parse("{entries[0].offset}"),
                        },
                        new FieldDefinition
                        {
                            Name = "second_size",
                            Type = FieldType.Virtual,
                            ValueExpression = ExpressionParser.Parse("{entries[1].size}"),
                        },
                    ],
                },
                ["entry"] = new()
                {
                    Name = "entry",
                    Fields =
                    [
                        new FieldDefinition { Name = "offset", Type = FieldType.UInt8 },
                        new FieldDefinition { Name = "size", Type = FieldType.UInt8 },
                    ],
                },
            },
            RootStruct = "main",
        };

        // data: [entry0.offset=0x10, entry0.size=0x20, entry1.offset=0x30, entry1.size=0x40]
        var data = new byte[] { 0x10, 0x20, 0x30, 0x40 };
        var result = _decoder.Decode(data, format);

        var firstOffset = result.Children[1].Should().BeOfType<DecodedVirtual>().Subject;
        firstOffset.Value.Should().Be(0x10L);

        var secondSize = result.Children[2].Should().BeOfType<DecodedVirtual>().Subject;
        secondSize.Value.Should().Be(0x40L);
    }

    [Fact]
    public void NestedStruct_DeepMemberAccess()
    {
        // {parent.child.value} でネスト構造体のフィールドを参照
        var format = new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["main"] = new()
                {
                    Name = "main",
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "outer",
                            Type = FieldType.Struct,
                            StructRef = "outer_struct",
                        },
                        new FieldDefinition
                        {
                            Name = "deep_val",
                            Type = FieldType.Virtual,
                            ValueExpression = ExpressionParser.Parse("{outer.inner.value}"),
                        },
                    ],
                },
                ["outer_struct"] = new()
                {
                    Name = "outer_struct",
                    Fields =
                    [
                        new FieldDefinition { Name = "tag", Type = FieldType.UInt8 },
                        new FieldDefinition
                        {
                            Name = "inner",
                            Type = FieldType.Struct,
                            StructRef = "inner_struct",
                        },
                    ],
                },
                ["inner_struct"] = new()
                {
                    Name = "inner_struct",
                    Fields =
                    [
                        new FieldDefinition { Name = "value", Type = FieldType.UInt8 },
                    ],
                },
            },
            RootStruct = "main",
        };

        // data: [tag=0x01, inner.value=0x42]
        var data = new byte[] { 0x01, 0x42 };
        var result = _decoder.Decode(data, format);

        var deepVal = result.Children[1].Should().BeOfType<DecodedVirtual>().Subject;
        deepVal.Value.Should().Be(0x42L);
    }

    [Fact]
    public void StructArraySeek_WithMemberAccess()
    {
        // seek: "{entries[_index].offset}" で構造体テーブルseek
        var format = new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["main"] = new()
                {
                    Name = "main",
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "entries",
                            Type = FieldType.Struct,
                            StructRef = "entry",
                            Repeat = new RepeatMode.Count(ExpressionParser.Parse("{2}")),
                        },
                        new FieldDefinition
                        {
                            Name = "data",
                            Type = FieldType.UInt8,
                            Repeat = new RepeatMode.Count(ExpressionParser.Parse("{2}")),
                            SeekExpression = ExpressionParser.Parse("{entries[_index].offset}"),
                            SeekRestore = true,
                        },
                    ],
                },
                ["entry"] = new()
                {
                    Name = "entry",
                    Fields =
                    [
                        new FieldDefinition { Name = "offset", Type = FieldType.UInt8 },
                        new FieldDefinition { Name = "size", Type = FieldType.UInt8 },
                    ],
                },
            },
            RootStruct = "main",
        };

        // data: [entry0.offset=6, entry0.size=1, entry1.offset=7, entry1.size=1, pad, pad, 0xAA, 0xBB]
        var data = new byte[] { 0x06, 0x01, 0x07, 0x01, 0x00, 0x00, 0xAA, 0xBB };
        var result = _decoder.Decode(data, format);

        var dataArray = result.Children[1].Should().BeOfType<DecodedArray>().Subject;
        dataArray.Elements.Should().HaveCount(2);
        dataArray.Elements[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0xAA);
        dataArray.Elements[1].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0xBB);
    }

    [Fact]
    public void ExistingScalarArrayIndex_StillWorks()
    {
        // 既存のスカラ配列 {arr[i]} が引き続き動作（回帰テスト）
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "offsets",
                Type = FieldType.UInt8,
                Repeat = new RepeatMode.Count(ExpressionParser.Parse("{3}")),
            },
            new FieldDefinition
            {
                Name = "second",
                Type = FieldType.Virtual,
                ValueExpression = ExpressionParser.Parse("{offsets[1]}"),
            });

        var data = new byte[] { 0x0A, 0x0B, 0x0C };
        var result = _decoder.Decode(data, format);

        var v = result.Children[1].Should().BeOfType<DecodedVirtual>().Subject;
        v.Value.Should().Be(0x0BL);
    }

    [Fact]
    public void SwitchField_RegisteredAsDictionary()
    {
        // switch型フィールドの結果もメンバーアクセス可能
        var format = new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["main"] = new()
                {
                    Name = "main",
                    Fields =
                    [
                        new FieldDefinition { Name = "type_id", Type = FieldType.UInt8 },
                        new FieldDefinition
                        {
                            Name = "payload",
                            Type = FieldType.Switch,
                            SwitchOn = ExpressionParser.Parse("{type_id}"),
                            SwitchCases =
                            [
                                new SwitchCase(ExpressionParser.Parse("{1}"), "type_a"),
                            ],
                            SwitchDefault = "type_a",
                        },
                        new FieldDefinition
                        {
                            Name = "extracted",
                            Type = FieldType.Virtual,
                            ValueExpression = ExpressionParser.Parse("{payload.value}"),
                        },
                    ],
                },
                ["type_a"] = new()
                {
                    Name = "type_a",
                    Fields =
                    [
                        new FieldDefinition { Name = "value", Type = FieldType.UInt8 },
                    ],
                },
            },
            RootStruct = "main",
        };

        // data: [type_id=1, value=0x55]
        var data = new byte[] { 0x01, 0x55 };
        var result = _decoder.Decode(data, format);

        var extracted = result.Children[2].Should().BeOfType<DecodedVirtual>().Subject;
        extracted.Value.Should().Be(0x55L);
    }

    private static FormatDefinition CreateFormat(string rootName, params FieldDefinition[] fields)
    {
        return new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                [rootName] = new()
                {
                    Name = rootName,
                    Fields = fields.ToList(),
                },
            },
            RootStruct = rootName,
        };
    }
}
