using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>
/// REQ-138: repeat ループ内の _prev 変数による前要素参照
/// </summary>
public class PrevVariableTests
{
    private readonly BinaryDecoder _decoder = new();

    /// <summary>
    /// スカラー repeat で _prev が前要素の値を返すこと
    /// </summary>
    [Fact]
    public void RepeatCount_ScalarPrev()
    {
        // uint8 x 3 の repeat。virtual フィールドで _prev を記録し、
        // 前要素の値が取れることを確認。
        // 構造体でラップして virtual で _prev を記録する。
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
                            Name = "values",
                            Type = FieldType.UInt8,
                            Repeat = new RepeatMode.Count(ExpressionParser.Parse("{3}")),
                        },
                        // _prev は repeat 終了後、最後の要素の値を保持
                        new FieldDefinition
                        {
                            Name = "last_prev",
                            Type = FieldType.Virtual,
                            ValueExpression = ExpressionParser.Parse("{_prev}"),
                        },
                    ],
                },
            },
            RootStruct = "main",
        };

        // values: 10, 20, 30
        var data = new byte[] { 10, 20, 30 };

        var result = _decoder.Decode(data, format);

        // _prev はループ終了後、最後の要素(30)の値を保持
        var lastPrev = result.Children[1].Should().BeOfType<DecodedVirtual>().Subject;
        lastPrev.Value.Should().Be(30L);
    }

    /// <summary>
    /// 構造体 repeat で _prev.field によるメンバーアクセスができること
    /// </summary>
    [Fact]
    public void RepeatCount_StructPrev_MemberAccess()
    {
        // 構造体(val: uint8) x 3。2番目以降の要素で _prev.val を virtual で記録。
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
                            Name = "items",
                            Type = FieldType.Struct,
                            StructRef = "item",
                            Repeat = new RepeatMode.Count(ExpressionParser.Parse("{3}")),
                        },
                    ],
                },
                ["item"] = new()
                {
                    Name = "item",
                    Fields =
                    [
                        new FieldDefinition { Name = "val", Type = FieldType.UInt8 },
                        // _index > 0 の場合のみ _prev.val を記録
                        new FieldDefinition
                        {
                            Name = "prev_val",
                            Type = FieldType.Virtual,
                            ValueExpression = ExpressionParser.Parse("{_prev.val}"),
                            Condition = ExpressionParser.Parse("{_index > 0}"),
                        },
                    ],
                },
            },
            RootStruct = "main",
        };

        // items: val=10, val=20, val=30
        var data = new byte[] { 10, 20, 30 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);

        // 要素0: prev_val は条件 false でスキップ
        var item0 = array.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        item0.Children.Should().HaveCount(1); // val のみ

        // 要素1: prev_val = 10 (要素0のval)
        var item1 = array.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        item1.Children.Should().HaveCount(2);
        var prevVal1 = item1.Children[1].Should().BeOfType<DecodedVirtual>().Subject;
        prevVal1.Value.Should().Be(10L);

        // 要素2: prev_val = 20 (要素1のval)
        var item2 = array.Elements[2].Should().BeOfType<DecodedStruct>().Subject;
        item2.Children.Should().HaveCount(2);
        var prevVal2 = item2.Children[1].Should().BeOfType<DecodedVirtual>().Subject;
        prevVal2.Value.Should().Be(20L);
    }

    /// <summary>
    /// _index == 0 で _prev を参照すると例外が発生すること
    /// </summary>
    [Fact]
    public void PrevNotAvailableAtIndex0()
    {
        // ガードなしで _prev を参照 → _index == 0 でエラー
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
                            Name = "items",
                            Type = FieldType.Struct,
                            StructRef = "item",
                            Repeat = new RepeatMode.Count(ExpressionParser.Parse("{2}")),
                        },
                    ],
                },
                ["item"] = new()
                {
                    Name = "item",
                    Fields =
                    [
                        new FieldDefinition { Name = "val", Type = FieldType.UInt8 },
                        // ガードなしで _prev を参照
                        new FieldDefinition
                        {
                            Name = "prev_val",
                            Type = FieldType.Virtual,
                            ValueExpression = ExpressionParser.Parse("{_prev}"),
                        },
                    ],
                },
            },
            RootStruct = "main",
        };

        var data = new byte[] { 10, 20 };

        // _index == 0 で _prev が未定義のため例外
        var act = () => _decoder.Decode(data, format);
        act.Should().Throw<Exception>();
    }

    /// <summary>
    /// if: "{_index > 0}" でガードすれば _index == 0 でもエラーにならないこと
    /// </summary>
    [Fact]
    public void PrevWithIfGuard()
    {
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
                            Name = "items",
                            Type = FieldType.Struct,
                            StructRef = "item",
                            Repeat = new RepeatMode.Count(ExpressionParser.Parse("{3}")),
                        },
                    ],
                },
                ["item"] = new()
                {
                    Name = "item",
                    Fields =
                    [
                        new FieldDefinition { Name = "val", Type = FieldType.UInt8 },
                        new FieldDefinition
                        {
                            Name = "prev_val",
                            Type = FieldType.Virtual,
                            ValueExpression = ExpressionParser.Parse("{_prev.val}"),
                            Condition = ExpressionParser.Parse("{_index > 0}"),
                        },
                    ],
                },
            },
            RootStruct = "main",
        };

        // items: val=5, val=15, val=25
        var data = new byte[] { 5, 15, 25 };

        // ガードにより _index == 0 ではスキップ → エラーなし
        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);

        // 要素0: prev_val スキップ
        var item0 = array.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        item0.Children.Should().HaveCount(1);

        // 要素1: prev_val = 5
        var item1 = array.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        item1.Children.Should().HaveCount(2);
        item1.Children[1].Should().BeOfType<DecodedVirtual>().Which.Value.Should().Be(5L);

        // 要素2: prev_val = 15
        var item2 = array.Elements[2].Should().BeOfType<DecodedStruct>().Subject;
        item2.Children.Should().HaveCount(2);
        item2.Children[1].Should().BeOfType<DecodedVirtual>().Which.Value.Should().Be(15L);
    }

    /// <summary>
    /// repeat: eof での _prev 動作確認
    /// </summary>
    [Fact]
    public void RepeatUntilEof_Prev()
    {
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
                            Name = "items",
                            Type = FieldType.Struct,
                            StructRef = "item",
                            Repeat = new RepeatMode.UntilEof(),
                        },
                    ],
                },
                ["item"] = new()
                {
                    Name = "item",
                    Fields =
                    [
                        new FieldDefinition { Name = "val", Type = FieldType.UInt8 },
                        new FieldDefinition
                        {
                            Name = "prev_val",
                            Type = FieldType.Virtual,
                            ValueExpression = ExpressionParser.Parse("{_prev.val}"),
                            Condition = ExpressionParser.Parse("{_index > 0}"),
                        },
                    ],
                },
            },
            RootStruct = "main",
        };

        // items: val=0xAA, val=0xBB, val=0xCC
        var data = new byte[] { 0xAA, 0xBB, 0xCC };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);

        // 要素0: prev_val スキップ
        var item0 = array.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        item0.Children.Should().HaveCount(1);

        // 要素1: prev_val = 0xAA (170)
        var item1 = array.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        item1.Children.Should().HaveCount(2);
        item1.Children[1].Should().BeOfType<DecodedVirtual>().Which.Value.Should().Be(0xAAL);

        // 要素2: prev_val = 0xBB (187)
        var item2 = array.Elements[2].Should().BeOfType<DecodedStruct>().Subject;
        item2.Children.Should().HaveCount(2);
        item2.Children[1].Should().BeOfType<DecodedVirtual>().Which.Value.Should().Be(0xBBL);
    }
}
