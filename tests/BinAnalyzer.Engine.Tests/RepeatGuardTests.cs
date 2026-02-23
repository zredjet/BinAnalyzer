using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class RepeatGuardTests
{
    private readonly BinaryDecoder _decoder = new();

    // --- repeat_max with Count mode ---

    [Fact]
    public void RepeatMax_Count_TruncatesAtLimit()
    {
        // repeat_count=100, repeat_max="3" → 3要素で打切り
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt8 }],
            mainFields:
            [
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "item",
                    Repeat = new RepeatMode.Count(ExpressionParser.Parse("{100}")),
                    RepeatMax = ExpressionParser.Parse("{3}"),
                },
            ]);

        // 10バイト用意するが3要素で打ち切り
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);
        array.Truncated.Should().BeTrue();
        array.TruncationReason.Should().Contain("repeat_max");
    }

    // --- repeat_max with UntilEof mode ---

    [Fact]
    public void RepeatMax_UntilEof_TruncatesAtLimit()
    {
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt8 }],
            mainFields:
            [
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "item",
                    Repeat = new RepeatMode.UntilEof(),
                    RepeatMax = ExpressionParser.Parse("{2}"),
                },
            ]);

        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);
        array.Truncated.Should().BeTrue();
        array.TruncationReason.Should().Contain("repeat_max");
    }

    // --- repeat_max with While mode ---

    [Fact]
    public void RepeatMax_While_TruncatesAtLimit()
    {
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt8 }],
            mainFields:
            [
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "item",
                    Repeat = new RepeatMode.While(ExpressionParser.Parse("{remaining > 0}")),
                    RepeatMax = ExpressionParser.Parse("{2}"),
                },
            ]);

        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);
        array.Truncated.Should().BeTrue();
        array.TruncationReason.Should().Contain("repeat_max");
    }

    // --- repeat_max with UntilValue mode ---

    [Fact]
    public void RepeatMax_UntilValue_TruncatesAtLimit()
    {
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt8 }],
            mainFields:
            [
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "item",
                    Repeat = new RepeatMode.UntilValue(ExpressionParser.Parse("{value == 0xFF}")),
                    RepeatMax = ExpressionParser.Parse("{2}"),
                },
            ]);

        // 0xFF (終端値) は3番目にあるが、repeat_max=2で先に打ち切られる
        var data = new byte[] { 0x01, 0x02, 0xFF, 0x04 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);
        array.Truncated.Should().BeTrue();
        array.TruncationReason.Should().Contain("repeat_max");
    }

    // --- repeat_max with LengthPrefixed mode ---

    [Fact]
    public void RepeatMax_LengthPrefixed_TruncatesAtLimit()
    {
        var format = CreateFormat("main", new StructDefinition
        {
            Name = "main",
            Fields =
            [
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Bytes,
                    Repeat = new RepeatMode.LengthPrefixed(1),
                    RepeatMax = ExpressionParser.Parse("{2}"),
                },
            ],
        });

        // 3要素: prefix=2,data=[AA,BB], prefix=1,data=[CC], prefix=1,data=[DD]
        var data = new byte[] { 0x02, 0xAA, 0xBB, 0x01, 0xCC, 0x01, 0xDD };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);
        array.Truncated.Should().BeTrue();
        array.TruncationReason.Should().Contain("repeat_max");
    }

    // --- repeat_error_limit ---

    [Fact]
    public void RepeatErrorLimit_TruncatesOnConsecutiveErrors()
    {
        // 要素struct: uint64 (8バイト必要) → 7バイト以下のデータ → 毎回エラー
        var elementStruct = new StructDefinition
        {
            Name = "chunk",
            Fields = [new FieldDefinition { Name = "big", Type = FieldType.UInt64 }],
            ResyncMarker = [0xAA],
        };

        var rootStruct = new StructDefinition
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
                    RepeatErrorLimit = ExpressionParser.Parse("{3}"),
                },
            ],
        };

        // 7バイトのデータ（uint64の8バイトに満たない）にAAマーカーを配置
        // 各要素デコードは必ず失敗し、resyncでAAマーカーへスキップ
        // pos 0→fail→resync to 1, pos 1→fail→resync to 3, pos 3→fail→3連続→truncated
        var data = new byte[] { 0x01, 0xAA, 0x02, 0xAA, 0x03, 0xAA, 0x04 };
        var format = CreateFormatMultiStruct("main", ("main", rootStruct), ("chunk", elementStruct));

        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);

        var array = result.Root.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Truncated.Should().BeTrue();
        array.TruncationReason.Should().Contain("repeat_error_limit");
    }

    [Fact]
    public void RepeatErrorLimit_ResetsOnSuccess()
    {
        // 1要素: magic(2 bytes) + value(uint8)
        var elementStruct = new StructDefinition
        {
            Name = "chunk",
            Fields =
            [
                new FieldDefinition { Name = "magic", Type = FieldType.Ascii, Size = 2 },
                new FieldDefinition { Name = "value", Type = FieldType.UInt8 },
            ],
            ResyncMarker = [0x4D, 0x4B], // "MK"
        };

        var rootStruct = new StructDefinition
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
                    RepeatErrorLimit = ExpressionParser.Parse("{3}"),
                },
            ],
        };

        // Good → Error → Good → Error → Error → (still < 3 consecutive)
        // Good: MK + value=01 (3 bytes)
        // Error: FF FF FF → resync to next MK
        // Good: MK + value=02 (3 bytes) → resets counter
        // Error: FF FF → not enough errors to trigger limit
        var data = new byte[]
        {
            0x4D, 0x4B, 0x01,       // good element
            0xFF, 0xFF, 0xFF,        // error (resync finds next MK)
            0x4D, 0x4B, 0x02,       // good element → counter resets
            0xFF, 0xFF,              // error
            0x4D, 0x4B, 0x03,       // good element after single error
        };
        var format = CreateFormatMultiStruct("main", ("main", rootStruct), ("chunk", elementStruct));

        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);

        var array = result.Root.Children[0].Should().BeOfType<DecodedArray>().Subject;
        // Should NOT be truncated because consecutive errors never reached 3
        array.Truncated.Should().BeFalse();
        // Should have successful elements
        array.Elements.OfType<DecodedStruct>().Should().HaveCountGreaterThanOrEqualTo(2);
    }

    // --- Global MaxRepeat via DecodeOptions ---

    [Fact]
    public void GlobalMaxRepeat_AppliesAsDefault()
    {
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt8 }],
            mainFields:
            [
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "item",
                    Repeat = new RepeatMode.UntilEof(),
                    // RepeatMax not set on field
                },
            ]);

        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        var options = new DecodeOptions { MaxRepeat = 5 };

        var result = _decoder.Decode(data, format, options);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(5);
        array.Truncated.Should().BeTrue();
    }

    [Fact]
    public void FieldRepeatMax_OverridesGlobal()
    {
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt8 }],
            mainFields:
            [
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "item",
                    Repeat = new RepeatMode.UntilEof(),
                    RepeatMax = ExpressionParser.Parse("{3}"),
                },
            ]);

        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        var options = new DecodeOptions { MaxRepeat = 100 };

        var result = _decoder.Decode(data, format, options);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);
        array.Truncated.Should().BeTrue();
    }

    // --- No guard: normal behavior unchanged ---

    [Fact]
    public void NoGuard_NormalBehaviorUnchanged()
    {
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt8 }],
            mainFields:
            [
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "item",
                    Repeat = new RepeatMode.UntilEof(),
                    // No RepeatMax, no RepeatErrorLimit
                },
            ]);

        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(4);
        array.Truncated.Should().BeFalse();
        array.TruncationReason.Should().BeNull();
    }

    // --- Dynamic expression for repeat_max ---

    [Fact]
    public void RepeatMax_Expression_Dynamic()
    {
        // repeat_max は "{limit}" 式で動的に評価される
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt8 }],
            mainFields:
            [
                new FieldDefinition { Name = "limit", Type = FieldType.UInt8 },
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "item",
                    Repeat = new RepeatMode.UntilEof(),
                    RepeatMax = ExpressionParser.Parse("{limit}"),
                },
            ]);

        // limit=3, then 5 item bytes
        var data = new byte[] { 0x03, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E };

        var result = _decoder.Decode(data, format);

        var array = result.Children[1].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);
        array.Truncated.Should().BeTrue();
    }

    // --- Truncation reason text ---

    [Fact]
    public void TruncationReason_Count_IncludesOriginalCount()
    {
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt8 }],
            mainFields:
            [
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "item",
                    Repeat = new RepeatMode.Count(ExpressionParser.Parse("{50}")),
                    RepeatMax = ExpressionParser.Parse("{3}"),
                },
            ]);

        var data = new byte[50];

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.TruncationReason.Should().Contain("exceeded original count");
    }

    [Fact]
    public void TruncationReason_UntilEof_IncludesReached()
    {
        var format = CreateFormatWithStruct("main", "item",
            itemFields: [new FieldDefinition { Name = "value", Type = FieldType.UInt8 }],
            mainFields:
            [
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "item",
                    Repeat = new RepeatMode.UntilEof(),
                    RepeatMax = ExpressionParser.Parse("{2}"),
                },
            ]);

        var data = new byte[] { 0x01, 0x02, 0x03 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.TruncationReason.Should().Contain("reached");
    }

    // --- ヘルパー ---

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

    private static FormatDefinition CreateFormat(string rootName, StructDefinition rootStruct)
    {
        return new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                [rootName] = rootStruct,
            },
            RootStruct = rootName,
        };
    }

    private static FormatDefinition CreateFormatMultiStruct(
        string rootName,
        params (string name, StructDefinition def)[] structs)
    {
        var dict = new Dictionary<string, StructDefinition>();
        foreach (var (name, def) in structs)
            dict[name] = def;

        return new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = dict,
            RootStruct = rootName,
        };
    }
}
