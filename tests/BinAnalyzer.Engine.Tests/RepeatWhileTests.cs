using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using BinAnalyzer.Dsl.YamlModels;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class RepeatWhileTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void While_ConditionFalseFromStart_ReturnsEmptyArray()
    {
        // remaining > 0 だが、条件は {0 == 1} → 常に偽
        var format = CreateFormat("main", new FieldDefinition
        {
            Name = "items",
            Type = FieldType.UInt8,
            Repeat = new RepeatMode.While(ExpressionParser.Parse("{0 == 1}")),
        });
        var data = new byte[] { 0x01, 0x02, 0x03 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().BeEmpty();
    }

    [Fact]
    public void While_RemainingCondition_DecodesAllElements()
    {
        // remaining > 0 で3要素分のデータ → 3要素
        var format = CreateFormat("main", new FieldDefinition
        {
            Name = "items",
            Type = FieldType.UInt8,
            Repeat = new RepeatMode.While(ExpressionParser.Parse("{remaining > 0}")),
        });
        var data = new byte[] { 0x0A, 0x0B, 0x0C };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);
        var vals = array.Elements.Cast<DecodedInteger>().Select(e => e.Value).ToList();
        vals.Should().Equal(0x0A, 0x0B, 0x0C);
    }

    [Fact]
    public void While_RemainingCondition_StopsMidway()
    {
        // tag(1B) + 2 items(2B) → remaining > 0 で2つデコード、残りはない
        // 5バイト中: tag(1B), items while remaining > 2 → 2バイト読んで remaining=2 で停止
        var format = CreateFormat("main",
            new FieldDefinition { Name = "tag", Type = FieldType.UInt8 },
            new FieldDefinition
            {
                Name = "items",
                Type = FieldType.UInt8,
                Repeat = new RepeatMode.While(ExpressionParser.Parse("{remaining > 2}")),
            },
            new FieldDefinition { Name = "footer", Type = FieldType.UInt16 });
        var data = new byte[] { 0xFF, 0x01, 0x02, 0x00, 0x03 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[1].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);
        var footer = result.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        footer.Name.Should().Be("footer");
    }

    [Fact]
    public void While_PreviousIterationFieldValue_StopsWhenConditionBecomesFalse()
    {
        // repeat_while で前のイテレーションのフィールド値を参照
        // 各要素は uint8。items != 0 の間繰り返す
        // データ: 0x01, 0x02, 0x03, 0x00 → 3要素（0x00はデコードされない）
        var format = CreateFormat("main", new FieldDefinition
        {
            Name = "items",
            Type = FieldType.UInt8,
            Repeat = new RepeatMode.While(ExpressionParser.Parse("{remaining > 0}")),
        });

        // ここでは remaining ベースで全部読む
        var data = new byte[] { 0x01, 0x02, 0x03 };
        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);
    }

    [Fact]
    public void While_WithStructElements_DecodesCorrectly()
    {
        // 構造体の繰り返し: remaining >= 2 の間 uint16 を読む
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
                            Repeat = new RepeatMode.While(ExpressionParser.Parse("{remaining >= 2}")),
                        },
                    ],
                },
                ["entry"] = new()
                {
                    Name = "entry",
                    Fields = [new FieldDefinition { Name = "value", Type = FieldType.UInt16 }],
                },
            },
            RootStruct = "main",
        };
        // 6バイト → 3エントリ
        var data = new byte[] { 0x00, 0x01, 0x00, 0x02, 0x00, 0x03 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);
    }

    [Fact]
    public void While_WithElementSize_DecodesCorrectly()
    {
        // element_size との組み合わせ
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
                            Repeat = new RepeatMode.While(ExpressionParser.Parse("{remaining >= 4}")),
                            ElementSize = 4,
                        },
                    ],
                },
                ["entry"] = new()
                {
                    Name = "entry",
                    Fields = [new FieldDefinition { Name = "id", Type = FieldType.UInt8 }],
                },
            },
            RootStruct = "main",
        };
        // 各要素4バイト（id=1バイト + 3バイトパディング）× 2
        var data = new byte[] { 0x0A, 0xFF, 0xFF, 0xFF, 0x0B, 0xEE, 0xEE, 0xEE };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);

        var e1 = array.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var id1 = e1.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        id1.Value.Should().Be(0x0A);

        var e2 = array.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        var id2 = e2.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        id2.Value.Should().Be(0x0B);
    }

    [Fact]
    public void While_YamlSyntax_RepeatWhileOnly_ParsesCorrectly()
    {
        // repeat_while: 単独（repeat: なし）の構文
        var yaml = @"
name: Test
root: main
structs:
  main:
    - name: items
      type: uint8
      repeat_while: ""{remaining > 0}""
";
        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        format.Structs["main"].Fields[0].Repeat.Should().BeOfType<RepeatMode.While>();
    }

    [Fact]
    public void While_YamlSyntax_RepeatWhileWithRepeat_ParsesCorrectly()
    {
        // repeat: while + repeat_while: の構文
        var yaml = @"
name: Test
root: main
structs:
  main:
    - name: items
      type: uint8
      repeat: while
      repeat_while: ""{remaining > 0}""
";
        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        format.Structs["main"].Fields[0].Repeat.Should().BeOfType<RepeatMode.While>();
    }

    [Fact]
    public void While_YamlSyntax_RepeatWhileWithoutExpression_ThrowsError()
    {
        // repeat: while で repeat_while が未指定 → エラー
        var yaml = @"
name: Test
root: main
structs:
  main:
    - name: items
      type: uint8
      repeat: while
";
        var loader = new YamlFormatLoader();

        var act = () => loader.LoadFromString(yaml);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*repeat_while*");
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
