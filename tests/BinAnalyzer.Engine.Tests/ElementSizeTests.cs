using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class ElementSizeTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void FixedElementSize_SkipsRemainingBytes()
    {
        // 各要素4バイト、構造体は2バイト（uint16）のみ → 残り2バイトはスキップ
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt16 }],
            mainFields: [new FieldDefinition
            {
                Name = "items",
                Type = FieldType.Struct,
                StructRef = "item",
                Repeat = new RepeatMode.Count(ExpressionParser.Parse("{2}")),
                ElementSize = 4,
            }]);

        // 要素1: [0x00, 0x01] + 2バイトパディング
        // 要素2: [0x00, 0x02] + 2バイトパディング
        var data = new byte[] { 0x00, 0x01, 0xFF, 0xFF, 0x00, 0x02, 0xEE, 0xEE };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);

        var item1 = array.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var val1 = item1.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        val1.Value.Should().Be(1);

        var item2 = array.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        var val2 = item2.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        val2.Value.Should().Be(2);

        array.Size.Should().Be(8);
    }

    [Fact]
    public void ExpressionElementSize_UsesEvaluatedValue()
    {
        // entry_sizeフィールドで要素サイズを指定
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "id", Type = FieldType.UInt8 }],
            mainFields:
            [
                new FieldDefinition { Name = "entry_size", Type = FieldType.UInt8 },
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "item",
                    Repeat = new RepeatMode.Count(ExpressionParser.Parse("{2}")),
                    ElementSizeExpression = ExpressionParser.Parse("{entry_size}"),
                },
            ]);

        // entry_size = 3, 要素1: [0x0A] + 2バイトパディング, 要素2: [0x0B] + 2バイトパディング
        var data = new byte[] { 0x03, 0x0A, 0x00, 0x00, 0x0B, 0x00, 0x00 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[1].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);

        var item1 = array.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var val1 = item1.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        val1.Value.Should().Be(0x0A);

        var item2 = array.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        var val2 = item2.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        val2.Value.Should().Be(0x0B);
    }

    [Fact]
    public void ElementSize_Overflow_ThrowsError()
    {
        // 要素サイズ2バイトだが、構造体は4バイト（uint32）を読もうとする
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt32 }],
            mainFields: [new FieldDefinition
            {
                Name = "items",
                Type = FieldType.Struct,
                StructRef = "item",
                Repeat = new RepeatMode.Count(ExpressionParser.Parse("{1}")),
                ElementSize = 2,
            }]);

        var data = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        var act = () => _decoder.Decode(data, format);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ElementSize_WithRepeatUntilEof()
    {
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt8 }],
            mainFields: [new FieldDefinition
            {
                Name = "items",
                Type = FieldType.Struct,
                StructRef = "item",
                Repeat = new RepeatMode.UntilEof(),
                ElementSize = 3,
            }]);

        // 3要素: [0x01, pad, pad], [0x02, pad, pad], [0x03, pad, pad]
        var data = new byte[] { 0x01, 0x00, 0x00, 0x02, 0x00, 0x00, 0x03, 0x00, 0x00 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);

        var val1 = ((DecodedStruct)array.Elements[0]).Children[0].Should().BeOfType<DecodedInteger>().Subject;
        val1.Value.Should().Be(1);
        var val2 = ((DecodedStruct)array.Elements[1]).Children[0].Should().BeOfType<DecodedInteger>().Subject;
        val2.Value.Should().Be(2);
        var val3 = ((DecodedStruct)array.Elements[2]).Children[0].Should().BeOfType<DecodedInteger>().Subject;
        val3.Value.Should().Be(3);
    }

    [Fact]
    public void ElementSize_WithRepeatUntil()
    {
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "type", Type = FieldType.UInt8 }],
            mainFields: [new FieldDefinition
            {
                Name = "items",
                Type = FieldType.Struct,
                StructRef = "item",
                Repeat = new RepeatMode.UntilValue(ExpressionParser.Parse("{type == 0}")),
                ElementSize = 4,
            }]);

        // 要素1: [0x01, pad, pad, pad], 要素2: [0x00, pad, pad, pad] (終端)
        var data = new byte[] { 0x01, 0xFF, 0xFF, 0xFF, 0x00, 0xEE, 0xEE, 0xEE };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);

        var val1 = ((DecodedStruct)array.Elements[0]).Children[0].Should().BeOfType<DecodedInteger>().Subject;
        val1.Value.Should().Be(1);
        var val2 = ((DecodedStruct)array.Elements[1]).Children[0].Should().BeOfType<DecodedInteger>().Subject;
        val2.Value.Should().Be(0);
    }

    [Fact]
    public void NoElementSize_BehaviorUnchanged()
    {
        // element_size未指定の場合は従来通り
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt8 }],
            mainFields: [new FieldDefinition
            {
                Name = "items",
                Type = FieldType.Struct,
                StructRef = "item",
                Repeat = new RepeatMode.Count(ExpressionParser.Parse("{3}")),
            }]);

        var data = new byte[] { 0x0A, 0x0B, 0x0C };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);
        array.Size.Should().Be(3);
    }

    private static FormatDefinition CreateFormatWithStruct(
        string rootName, string structName,
        FieldDefinition[] itemFields,
        FieldDefinition[] mainFields)
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
                    Fields = mainFields.ToList(),
                },
                [structName] = new()
                {
                    Name = structName,
                    Fields = itemFields.ToList(),
                },
            },
            RootStruct = rootName,
        };
    }
}
