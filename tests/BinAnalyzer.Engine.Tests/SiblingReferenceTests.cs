using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>
/// REQ-099: 兄弟チャンク参照 — 繰り返し配列内で前要素のスカラー変数を後続要素から参照できること
/// </summary>
public class SiblingReferenceTests
{
    private readonly BinaryDecoder _decoder = new();

    /// <summary>
    /// repeat: eof で構造体要素をデコードし、前要素のスカラーフィールドが
    /// 後続要素の switch で参照できること（受入条件 1, 2 に対応）
    /// </summary>
    [Fact]
    public void RepeatEof_StructElement_PromotesScalarVariables()
    {
        // chunk 構造体: id(ascii 4) + size(uint8) + data(switch on "{id}")
        // 要素0: id="AAAA", size=2, data → type_a(uint16)
        // 要素1: id="BBBB", size=1, data → type_b(uint8)
        //   switch_on: "{id}" で直前の要素0の id="AAAA" ではなく、
        //   自身のデコード時の id="BBBB" が参照されることを確認
        //   ※ 要素1のデコード時には、要素0の id もプロモート済みだが
        //     要素1自身の id が上書きするため "BBBB" が参照される

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
                            Name = "chunks",
                            Type = FieldType.Struct,
                            StructRef = "chunk",
                            Repeat = new RepeatMode.UntilEof(),
                        },
                    ],
                },
                ["chunk"] = new()
                {
                    Name = "chunk",
                    Fields =
                    [
                        new FieldDefinition { Name = "id", Type = FieldType.Ascii, Size = 4 },
                        new FieldDefinition { Name = "size", Type = FieldType.UInt8 },
                        new FieldDefinition
                        {
                            Name = "data",
                            Type = FieldType.Switch,
                            SwitchOn = ExpressionParser.Parse("{id}"),
                            SwitchCases = new[]
                            {
                                new SwitchCase(ExpressionParser.Parse("{'AAAA'}"), "type_a"),
                                new SwitchCase(ExpressionParser.Parse("{'BBBB'}"), "type_b"),
                            },
                            SizeExpression = ExpressionParser.Parse("{size}"),
                        },
                    ],
                },
                ["type_a"] = new()
                {
                    Name = "type_a",
                    Fields = [new FieldDefinition { Name = "value_a", Type = FieldType.UInt16 }],
                },
                ["type_b"] = new()
                {
                    Name = "type_b",
                    Fields = [new FieldDefinition { Name = "value_b", Type = FieldType.UInt8 }],
                },
            },
            RootStruct = "main",
        };

        // 要素0: "AAAA" + size=2 + uint16(0x0102)
        // 要素1: "BBBB" + size=1 + uint8(0xFF)
        var data = new byte[]
        {
            // chunk 0: id="AAAA", size=2, data=0x0102
            0x41, 0x41, 0x41, 0x41, 0x02, 0x01, 0x02,
            // chunk 1: id="BBBB", size=1, data=0xFF
            0x42, 0x42, 0x42, 0x42, 0x01, 0xFF,
        };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);

        // 要素0: type_a
        var chunk0 = array.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var data0 = chunk0.Children[2].Should().BeOfType<DecodedStruct>().Subject;
        data0.StructType.Should().Be("type_a");
        data0.Children[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0102);

        // 要素1: type_b（自身の id="BBBB" が使われる）
        var chunk1 = array.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        var data1 = chunk1.Children[2].Should().BeOfType<DecodedStruct>().Subject;
        data1.StructType.Should().Be("type_b");
        data1.Children[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0xFF);
    }

    /// <summary>
    /// repeat: count での動作確認
    /// </summary>
    [Fact]
    public void RepeatCount_StructElement_PromotesScalarVariables()
    {
        // 2つのチャンクを repeat: count(2) でデコード
        // 要素0 の kind フィールドを要素1 で参照できること
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
                            Name = "chunks",
                            Type = FieldType.Struct,
                            StructRef = "chunk",
                            Repeat = new RepeatMode.Count(ExpressionParser.Parse("{2}")),
                        },
                    ],
                },
                ["chunk"] = new()
                {
                    Name = "chunk",
                    Fields =
                    [
                        new FieldDefinition { Name = "kind", Type = FieldType.UInt8 },
                        new FieldDefinition { Name = "size", Type = FieldType.UInt8 },
                        new FieldDefinition
                        {
                            Name = "payload",
                            Type = FieldType.Switch,
                            SwitchOn = ExpressionParser.Parse("{kind}"),
                            SwitchCases = new[]
                            {
                                new SwitchCase(ExpressionParser.Parse("{1}"), "payload_a"),
                                new SwitchCase(ExpressionParser.Parse("{2}"), "payload_b"),
                            },
                            SizeExpression = ExpressionParser.Parse("{size}"),
                        },
                    ],
                },
                ["payload_a"] = new()
                {
                    Name = "payload_a",
                    Fields = [new FieldDefinition { Name = "val", Type = FieldType.UInt8 }],
                },
                ["payload_b"] = new()
                {
                    Name = "payload_b",
                    Fields = [new FieldDefinition { Name = "val", Type = FieldType.UInt16 }],
                },
            },
            RootStruct = "main",
        };

        // chunk0: kind=1, size=1, payload_a(uint8=0xAA)
        // chunk1: kind=2, size=2, payload_b(uint16=0x00BB)
        var data = new byte[]
        {
            0x01, 0x01, 0xAA,
            0x02, 0x02, 0x00, 0xBB,
        };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);

        var chunk0 = array.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        chunk0.Children[2].Should().BeOfType<DecodedStruct>().Which.StructType.Should().Be("payload_a");

        var chunk1 = array.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        chunk1.Children[2].Should().BeOfType<DecodedStruct>().Which.StructType.Should().Be("payload_b");
    }

    /// <summary>
    /// 後の要素が同名変数を上書きすることの確認（名前衝突のセマンティクス）
    /// </summary>
    [Fact]
    public void RepeatEof_StructElement_LaterElementOverwritesVariable()
    {
        // 3つのチャンクを repeat: eof でデコード
        // 各要素は tag(uint8) を持つ。後続要素は前要素の tag を上書きする。
        // 要素2 の switch で参照される tag は要素1 の tag ではなく要素2 自身の tag。
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
                        new FieldDefinition { Name = "tag", Type = FieldType.UInt8 },
                        new FieldDefinition
                        {
                            Name = "body",
                            Type = FieldType.Switch,
                            SwitchOn = ExpressionParser.Parse("{tag}"),
                            SwitchCases = new[]
                            {
                                new SwitchCase(ExpressionParser.Parse("{1}"), "body_one"),
                                new SwitchCase(ExpressionParser.Parse("{2}"), "body_two"),
                                new SwitchCase(ExpressionParser.Parse("{3}"), "body_three"),
                            },
                        },
                    ],
                },
                ["body_one"] = new()
                {
                    Name = "body_one",
                    Fields = [new FieldDefinition { Name = "v", Type = FieldType.UInt8 }],
                },
                ["body_two"] = new()
                {
                    Name = "body_two",
                    Fields = [new FieldDefinition { Name = "v", Type = FieldType.UInt8 }],
                },
                ["body_three"] = new()
                {
                    Name = "body_three",
                    Fields = [new FieldDefinition { Name = "v", Type = FieldType.UInt8 }],
                },
            },
            RootStruct = "main",
        };

        // item0: tag=1, body_one(v=0x0A)
        // item1: tag=2, body_two(v=0x0B)
        // item2: tag=3, body_three(v=0x0C)
        var data = new byte[] { 0x01, 0x0A, 0x02, 0x0B, 0x03, 0x0C };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);

        // 各要素が自身の tag に基づいて正しい body を選択していること
        array.Elements[0].Should().BeOfType<DecodedStruct>().Which
            .Children[1].Should().BeOfType<DecodedStruct>().Which.StructType.Should().Be("body_one");
        array.Elements[1].Should().BeOfType<DecodedStruct>().Which
            .Children[1].Should().BeOfType<DecodedStruct>().Which.StructType.Should().Be("body_two");
        array.Elements[2].Should().BeOfType<DecodedStruct>().Which
            .Children[1].Should().BeOfType<DecodedStruct>().Which.StructType.Should().Be("body_three");
    }

    /// <summary>
    /// boundary スコープ（switch + size）内の深いフィールドもプロモートされること（受入条件 1 の核心テスト）
    ///
    /// 要素0: header チャンク → switch(size付き) → header_struct → fccType="vids" が境界スコープ内で消滅
    /// 要素1: format チャンク → switch_on: "{fccType}" → fccType がプロモートされていれば成功
    /// </summary>
    [Fact]
    public void RepeatEof_NestedStructElement_PromotesDeeplyNestedScalars()
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
                            Name = "chunks",
                            Type = FieldType.Struct,
                            StructRef = "riff_chunk",
                            Repeat = new RepeatMode.UntilEof(),
                        },
                    ],
                },
                ["riff_chunk"] = new()
                {
                    Name = "riff_chunk",
                    Fields =
                    [
                        new FieldDefinition { Name = "chunk_id", Type = FieldType.Ascii, Size = 4 },
                        new FieldDefinition { Name = "chunk_size", Type = FieldType.UInt8 },
                        new FieldDefinition
                        {
                            Name = "data",
                            Type = FieldType.Switch,
                            SwitchOn = ExpressionParser.Parse("{chunk_id}"),
                            SwitchCases = new[]
                            {
                                new SwitchCase(ExpressionParser.Parse("{'strh'}"), "stream_header"),
                                new SwitchCase(ExpressionParser.Parse("{'strf'}"), "stream_format"),
                            },
                            SizeExpression = ExpressionParser.Parse("{chunk_size}"),
                        },
                    ],
                },
                ["stream_header"] = new()
                {
                    Name = "stream_header",
                    Fields =
                    [
                        new FieldDefinition { Name = "fccType", Type = FieldType.Ascii, Size = 4 },
                    ],
                },
                ["stream_format"] = new()
                {
                    Name = "stream_format",
                    Fields =
                    [
                        // fccType がプロモートされていれば参照可能
                        new FieldDefinition
                        {
                            Name = "fmt",
                            Type = FieldType.Switch,
                            SwitchOn = ExpressionParser.Parse("{fccType}"),
                            SwitchCases = new[]
                            {
                                new SwitchCase(ExpressionParser.Parse("{'vids'}"), "video_format"),
                                new SwitchCase(ExpressionParser.Parse("{'auds'}"), "audio_format"),
                            },
                            SizeRemaining = true,
                        },
                    ],
                },
                ["video_format"] = new()
                {
                    Name = "video_format",
                    Fields = [new FieldDefinition { Name = "width", Type = FieldType.UInt16 }],
                },
                ["audio_format"] = new()
                {
                    Name = "audio_format",
                    Fields = [new FieldDefinition { Name = "channels", Type = FieldType.UInt8 }],
                },
            },
            RootStruct = "main",
        };

        // chunk 0: "strh" + size=4 + fccType="vids"
        // chunk 1: "strf" + size=2 + switch on fccType → video_format → width=0x0140(320)
        var data = new byte[]
        {
            // strh chunk: id="strh", size=4, data="vids"
            0x73, 0x74, 0x72, 0x68, 0x04, 0x76, 0x69, 0x64, 0x73,
            // strf chunk: id="strf", size=2, data=width(0x0140)
            0x73, 0x74, 0x72, 0x66, 0x02, 0x01, 0x40,
        };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);

        // 要素1 の stream_format 内の switch が fccType="vids" で video_format を選択
        var chunk1 = array.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        var strf_data = chunk1.Children[2].Should().BeOfType<DecodedStruct>().Subject;
        strf_data.StructType.Should().Be("stream_format");

        var fmt = strf_data.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        fmt.StructType.Should().Be("video_format");
        fmt.Children[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0140);
    }

    /// <summary>
    /// スカラー配列（DecodedInteger の繰り返し）ではプロモートが発生しないこと（受入条件 5）
    /// </summary>
    [Fact]
    public void RepeatEof_ScalarElement_NoPromotion()
    {
        // スカラー uint8 の繰り返し → プロモートは DecodedStruct の場合のみ
        // REQ-098 の List<object> 保存は別途動作する
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
                            Repeat = new RepeatMode.UntilEof(),
                        },
                    ],
                },
            },
            RootStruct = "main",
        };

        var data = new byte[] { 0x0A, 0x0B, 0x0C };

        // スカラー配列は例外なくデコードされる（PromoteDecodedValues は呼ばれない）
        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);
        array.Elements[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0A);
        array.Elements[1].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0B);
        array.Elements[2].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0C);
    }

    /// <summary>
    /// seek + seek_restore パターンとの共存（受入条件 3）
    /// </summary>
    [Fact]
    public void SeekRestore_WithPromotion_StillWorks()
    {
        // 要素ごとに seek して構造体をデコードし、seek_restore で戻る
        // プロモートが seek/restore と干渉しないことを確認
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
                        // オフセットテーブル: 2つのオフセット (各 uint8)
                        new FieldDefinition { Name = "offset0", Type = FieldType.UInt8 },
                        new FieldDefinition { Name = "offset1", Type = FieldType.UInt8 },
                        // 構造体の繰り返し (seek + seek_restore)
                        new FieldDefinition
                        {
                            Name = "entries",
                            Type = FieldType.Struct,
                            StructRef = "entry",
                            Repeat = new RepeatMode.Count(ExpressionParser.Parse("{2}")),
                            SeekExpression = ExpressionParser.Parse("{_index == 0 ? offset0 : offset1}"),
                            SeekRestore = true,
                        },
                    ],
                },
                ["entry"] = new()
                {
                    Name = "entry",
                    Fields =
                    [
                        new FieldDefinition { Name = "tag", Type = FieldType.UInt8 },
                        new FieldDefinition { Name = "val", Type = FieldType.UInt8 },
                    ],
                },
            },
            RootStruct = "main",
        };

        // offset0=4, offset1=6
        // padding at 2,3
        // entry0 at offset 4: tag=0x01, val=0xAA
        // entry1 at offset 6: tag=0x02, val=0xBB
        var data = new byte[]
        {
            0x04, 0x06,       // offset table
            0xFF, 0xFF,       // padding
            0x01, 0xAA,       // entry0
            0x02, 0xBB,       // entry1
        };

        var result = _decoder.Decode(data, format);

        var array = result.Children[2].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);

        var entry0 = array.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        entry0.Children[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x01);
        entry0.Children[1].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0xAA);

        var entry1 = array.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        entry1.Children[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x02);
        entry1.Children[1].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0xBB);
    }
}
