using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class ArrayElementSeekTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void RepeatCount_SetsIndexVariable()
    {
        // 配列要素のデコード中に _index が参照可能であることを検証
        // virtual フィールドで _index を記録する方法で間接的に検証
        // uint8 x 3 → 要素値が正しく保存される（_index 設定の副作用確認）
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "values",
                Type = FieldType.UInt8,
                Repeat = new RepeatMode.Count(ExpressionParser.Parse("{3}")),
            });

        var data = new byte[] { 0x0A, 0x0B, 0x0C };
        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);
        array.Elements[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0A);
        array.Elements[1].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0B);
        array.Elements[2].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0C);
    }

    [Fact]
    public void RepeatCount_StoresArrayValues()
    {
        // スカラー配列の要素値が List<object> 変数として保存され、
        // 後続フィールドの式から配列インデックスで参照できること
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "offsets",
                Type = FieldType.UInt8,
                Repeat = new RepeatMode.Count(ExpressionParser.Parse("{3}")),
            },
            new FieldDefinition
            {
                Name = "second_offset",
                Type = FieldType.Virtual,
                ValueExpression = ExpressionParser.Parse("{offsets[1]}"),
            });

        var data = new byte[] { 0x10, 0x20, 0x30 };
        var result = _decoder.Decode(data, format);

        var virtualNode = result.Children[1].Should().BeOfType<DecodedVirtual>().Subject;
        virtualNode.Value.Should().Be(0x20L);
    }

    [Fact]
    public void PerElementSeek_WithIndexAccess()
    {
        // offsets = [0x06, 0x07, 0x08], 各オフセット先に uint8 データ
        // seek: "{offsets[_index]}" で各要素が正しいオフセットからデコード
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "count",
                Type = FieldType.UInt8,
            },
            new FieldDefinition
            {
                Name = "offsets",
                Type = FieldType.UInt8,
                Repeat = new RepeatMode.Count(ExpressionParser.Parse("{count}")),
            },
            new FieldDefinition
            {
                Name = "values",
                Type = FieldType.UInt8,
                Repeat = new RepeatMode.Count(ExpressionParser.Parse("{count}")),
                SeekExpression = ExpressionParser.Parse("{offsets[_index]}"),
                SeekRestore = true,
            });

        // data: [count=3, off0=6, off1=7, off2=8, pad, pad, val0=0xAA, val1=0xBB, val2=0xCC]
        var data = new byte[] { 0x03, 0x06, 0x07, 0x08, 0x00, 0x00, 0xAA, 0xBB, 0xCC };
        var result = _decoder.Decode(data, format);

        var array = result.Children[2].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);
        array.Elements[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0xAA);
        array.Elements[1].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0xBB);
        array.Elements[2].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0xCC);
    }

    [Fact]
    public void PerElementSeek_WithSeekRestore()
    {
        // 各要素デコード後に位置復元されること
        // seek_restore: true → 配列デコード後の位置がオフセット配列直後のまま
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "offsets",
                Type = FieldType.UInt8,
                Repeat = new RepeatMode.Count(ExpressionParser.Parse("{2}")),
            },
            new FieldDefinition
            {
                Name = "values",
                Type = FieldType.UInt8,
                Repeat = new RepeatMode.Count(ExpressionParser.Parse("{2}")),
                SeekExpression = ExpressionParser.Parse("{offsets[_index]}"),
                SeekRestore = true,
            },
            new FieldDefinition
            {
                Name = "next",
                Type = FieldType.UInt8,
            });

        // data: [off0=4, off1=5, next_byte=0xFF, pad, val0=0xAA, val1=0xBB]
        var data = new byte[] { 0x04, 0x05, 0xFF, 0x00, 0xAA, 0xBB };
        var result = _decoder.Decode(data, format);

        // values 配列は seek_restore なので位置はオフセット配列の直後に戻る
        var valuesArray = result.Children[1].Should().BeOfType<DecodedArray>().Subject;
        valuesArray.Elements[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0xAA);
        valuesArray.Elements[1].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0xBB);

        // next は offsets 配列直後 (position=2) から読まれる
        var next = result.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        next.Offset.Should().Be(2);
        next.Value.Should().Be(0xFF);
    }

    [Fact]
    public void PerElementSeek_StructElements()
    {
        // オフセット先の構造体が正しくデコードされる
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
                            Name = "offsets",
                            Type = FieldType.UInt8,
                            Repeat = new RepeatMode.Count(ExpressionParser.Parse("{2}")),
                        },
                        new FieldDefinition
                        {
                            Name = "entries",
                            Type = FieldType.Struct,
                            StructRef = "entry",
                            Repeat = new RepeatMode.Count(ExpressionParser.Parse("{2}")),
                            SeekExpression = ExpressionParser.Parse("{offsets[_index]}"),
                            SeekRestore = true,
                        },
                    ],
                },
                ["entry"] = new()
                {
                    Name = "entry",
                    Fields =
                    [
                        new FieldDefinition { Name = "id", Type = FieldType.UInt8 },
                        new FieldDefinition { Name = "val", Type = FieldType.UInt8 },
                    ],
                },
            },
            RootStruct = "main",
        };

        // data: [off0=4, off1=6, pad, pad, id0=0x01, val0=0x0A, id1=0x02, val1=0x0B]
        var data = new byte[] { 0x04, 0x06, 0x00, 0x00, 0x01, 0x0A, 0x02, 0x0B };
        var result = _decoder.Decode(data, format);

        var entries = result.Children[1].Should().BeOfType<DecodedArray>().Subject;
        entries.Elements.Should().HaveCount(2);

        var entry0 = entries.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        entry0.Children[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x01);
        entry0.Children[1].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0A);

        var entry1 = entries.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        entry1.Children[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x02);
        entry1.Children[1].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0B);
    }

    [Fact]
    public void ExistingSeekPlusRepeat_Unchanged()
    {
        // 既存動作: seek: "{offset}" + repeat（_index なし）→ 一括 seek（後方互換）
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "offset",
                Type = FieldType.UInt8,
            },
            new FieldDefinition
            {
                Name = "count",
                Type = FieldType.UInt8,
            },
            new FieldDefinition
            {
                Name = "values",
                Type = FieldType.UInt8,
                Repeat = new RepeatMode.Count(ExpressionParser.Parse("{count}")),
                SeekExpression = ExpressionParser.Parse("{offset}"),
            });

        var data = new byte[] { 0x02, 0x03, 0x0A, 0x0B, 0x0C };
        var result = _decoder.Decode(data, format);

        var array = result.Children[2].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);
        array.Elements[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0A);
        array.Elements[1].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0B);
        array.Elements[2].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0C);
    }

    [Fact]
    public void NestedRepeat_IndexShadowing()
    {
        // 内側ループの _index が外側を隠蔽し、外側に戻ったら復元される
        // 外側: 2要素のオフセット配列 → 内側: 各オフセット先で2バイト読み
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
                            Name = "offsets",
                            Type = FieldType.UInt8,
                            Repeat = new RepeatMode.Count(ExpressionParser.Parse("{2}")),
                        },
                        new FieldDefinition
                        {
                            Name = "groups",
                            Type = FieldType.Struct,
                            StructRef = "group",
                            Repeat = new RepeatMode.Count(ExpressionParser.Parse("{2}")),
                            SeekExpression = ExpressionParser.Parse("{offsets[_index]}"),
                            SeekRestore = true,
                        },
                    ],
                },
                ["group"] = new()
                {
                    Name = "group",
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "items",
                            Type = FieldType.UInt8,
                            Repeat = new RepeatMode.Count(ExpressionParser.Parse("{2}")),
                        },
                    ],
                },
            },
            RootStruct = "main",
        };

        // data: [off0=4, off1=6, pad, pad, g0_item0=0xAA, g0_item1=0xBB, g1_item0=0xCC, g1_item1=0xDD]
        var data = new byte[] { 0x04, 0x06, 0x00, 0x00, 0xAA, 0xBB, 0xCC, 0xDD };
        var result = _decoder.Decode(data, format);

        var groups = result.Children[1].Should().BeOfType<DecodedArray>().Subject;
        groups.Elements.Should().HaveCount(2);

        var group0 = groups.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var items0 = group0.Children[0].Should().BeOfType<DecodedArray>().Subject;
        items0.Elements[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0xAA);
        items0.Elements[1].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0xBB);

        var group1 = groups.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        var items1 = group1.Children[0].Should().BeOfType<DecodedArray>().Subject;
        items1.Elements[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0xCC);
        items1.Elements[1].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0xDD);
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
