using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class SizeRepeatCoexistenceTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void SizeWithRepeatEof_CreatesBoundaryScope()
    {
        // size: 6 + repeat: eof → 6バイト内の要素のみデコード、後続データに触れない
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt16 }],
            mainFields:
            [
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "item",
                    Size = 6,
                    Repeat = new RepeatMode.UntilEof(),
                },
                new FieldDefinition { Name = "trailer", Type = FieldType.UInt8 },
            ]);

        // items境界: [0x00,0x01, 0x00,0x02, 0x00,0x03] → 3要素, trailer: 0xAA
        var data = new byte[] { 0x00, 0x01, 0x00, 0x02, 0x00, 0x03, 0xAA };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);

        var val1 = ((DecodedStruct)array.Elements[0]).Children[0].Should().BeOfType<DecodedInteger>().Subject;
        val1.Value.Should().Be(1);
        var val2 = ((DecodedStruct)array.Elements[1]).Children[0].Should().BeOfType<DecodedInteger>().Subject;
        val2.Value.Should().Be(2);
        var val3 = ((DecodedStruct)array.Elements[2]).Children[0].Should().BeOfType<DecodedInteger>().Subject;
        val3.Value.Should().Be(3);

        // trailerが正しく読めること（境界スコープの外）
        var trailer = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        trailer.Value.Should().Be(0xAA);
    }

    [Fact]
    public void SizeExpressionWithRepeatEof_CreatesBoundaryScope()
    {
        // size: "{data_size}" + repeat: eof → 式評価された境界内でデコード
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt8 }],
            mainFields:
            [
                new FieldDefinition { Name = "data_size", Type = FieldType.UInt8 },
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "item",
                    SizeExpression = ExpressionParser.Parse("{data_size}"),
                    Repeat = new RepeatMode.UntilEof(),
                },
                new FieldDefinition { Name = "trailer", Type = FieldType.UInt8 },
            ]);

        // data_size=3, items: [0x0A, 0x0B, 0x0C], trailer: 0xFF
        var data = new byte[] { 0x03, 0x0A, 0x0B, 0x0C, 0xFF };

        var result = _decoder.Decode(data, format);

        var array = result.Children[1].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);

        var trailer = result.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        trailer.Value.Should().Be(0xFF);
    }

    [Fact]
    public void SizeRemainingWithRepeatEof_BehavesAsPlainRepeatEof()
    {
        // size: remaining + repeat: eof → 既存動作と同一（回帰テスト）
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt8 }],
            mainFields: [new FieldDefinition
            {
                Name = "items",
                Type = FieldType.Struct,
                StructRef = "item",
                SizeRemaining = true,
                Repeat = new RepeatMode.UntilEof(),
            }]);

        var data = new byte[] { 0x01, 0x02, 0x03 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);
    }

    [Fact]
    public void SizeWithRepeatCount_AdvancesToScopeEnd()
    {
        // size: 10 + repeat_count: 2 → 要素が6バイトしか消費しなくてもサイズは10
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt16 }],
            mainFields:
            [
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "item",
                    Size = 10,
                    Repeat = new RepeatMode.Count(ExpressionParser.Parse("{2}")),
                },
                new FieldDefinition { Name = "after", Type = FieldType.UInt8 },
            ]);

        // items境界: 10バイト [0x00,0x01, 0x00,0x02, + 6バイトパディング], after: 0xBB
        var data = new byte[] { 0x00, 0x01, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xBB };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);

        // PopScopeにより位置がスコープ終端(offset 10)まで進む
        var after = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        after.Value.Should().Be(0xBB);
    }

    [Fact]
    public void SizeWithRepeatWhile_RemainingRefersToScope()
    {
        // size: 6 + repeat_while: "{remaining >= 2}" → スコープ内のremainingで判定
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt16 }],
            mainFields:
            [
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "item",
                    Size = 6,
                    Repeat = new RepeatMode.While(ExpressionParser.Parse("{remaining >= 2}")),
                },
                new FieldDefinition { Name = "after", Type = FieldType.UInt8 },
            ]);

        // items境界: 6バイト [0x00,0x01, 0x00,0x02, 0x00,0x03], after: 0xCC
        var data = new byte[] { 0x00, 0x01, 0x00, 0x02, 0x00, 0x03, 0xCC };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);

        var after = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        after.Value.Should().Be(0xCC);
    }

    [Fact]
    public void SizeWithRepeatUntilValue_StopsAtConditionOrScopeEnd()
    {
        // size: 8 + repeat_until: "{type == 0}" → 条件一致またはスコープ終端で停止
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "type", Type = FieldType.UInt16 }],
            mainFields:
            [
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "item",
                    Size = 8,
                    Repeat = new RepeatMode.UntilValue(ExpressionParser.Parse("{type == 0}")),
                },
                new FieldDefinition { Name = "after", Type = FieldType.UInt8 },
            ]);

        // items: [0x00,0x01, 0x00,0x02, 0x00,0x00(=終端), pad, pad], after: 0xDD
        var data = new byte[] { 0x00, 0x01, 0x00, 0x02, 0x00, 0x00, 0xFF, 0xFF, 0xDD };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3); // 1, 2, 0(終端要素も含む)

        // PopScopeによりスコープ終端(offset 8)まで進む
        var after = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        after.Value.Should().Be(0xDD);
    }

    [Fact]
    public void SizeAndElementSize_BothWork()
    {
        // size: 9 + element_size: 3 + repeat: eof → 両方のスコープが機能
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt8 }],
            mainFields:
            [
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "item",
                    Size = 9,
                    ElementSize = 3,
                    Repeat = new RepeatMode.UntilEof(),
                },
                new FieldDefinition { Name = "after", Type = FieldType.UInt8 },
            ]);

        // items境界9バイト: [0x0A,pad,pad, 0x0B,pad,pad, 0x0C,pad,pad], after: 0xEE
        var data = new byte[] { 0x0A, 0x00, 0x00, 0x0B, 0x00, 0x00, 0x0C, 0x00, 0x00, 0xEE };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);

        var val1 = ((DecodedStruct)array.Elements[0]).Children[0].Should().BeOfType<DecodedInteger>().Subject;
        val1.Value.Should().Be(0x0A);
        var val2 = ((DecodedStruct)array.Elements[1]).Children[0].Should().BeOfType<DecodedInteger>().Subject;
        val2.Value.Should().Be(0x0B);
        var val3 = ((DecodedStruct)array.Elements[2]).Children[0].Should().BeOfType<DecodedInteger>().Subject;
        val3.Value.Should().Be(0x0C);

        var after = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        after.Value.Should().Be(0xEE);
    }

    [Fact]
    public void NoSizeWithRepeat_BehaviorUnchanged()
    {
        // size無し + repeat: eof → 既存動作と同一（回帰テスト）
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt8 }],
            mainFields: [new FieldDefinition
            {
                Name = "items",
                Type = FieldType.Struct,
                StructRef = "item",
                Repeat = new RepeatMode.UntilEof(),
            }]);

        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(4);
        array.Size.Should().Be(4);
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
