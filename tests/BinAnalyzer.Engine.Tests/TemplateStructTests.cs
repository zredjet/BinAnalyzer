using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class TemplateStructTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void NamedArgs_DecodesCorrectly()
    {
        // data_block(prefix_size=2, payload_size=3)
        // prefix=2bytes, payload=3bytes
        var format = CreateDataBlockFormat(
            prefixSizeDefault: 1, payloadSizeDefault: 1,
            args: [
                new StructArgument("prefix_size", 2, null),
                new StructArgument("payload_size", 3, null),
            ]);

        // prefix=0xAA,0xBB(2bytes), payload=0x01,0x02,0x03(3bytes)
        var data = new byte[] { 0xAA, 0xBB, 0x01, 0x02, 0x03 };
        var result = _decoder.Decode(data, format);

        var entry = result.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        entry.Children[0].Should().BeOfType<DecodedBytes>().Which.Size.Should().Be(2); // prefix
        entry.Children[1].Should().BeOfType<DecodedBytes>().Which.Size.Should().Be(3); // payload
    }

    [Fact]
    public void PositionalArgs_DecodesCorrectly()
    {
        // data_block(3, 2) — prefix_size=3, payload_size=2
        var format = CreateDataBlockFormat(
            prefixSizeDefault: 1, payloadSizeDefault: 1,
            args: [
                new StructArgument(null, 3, null), // prefix_size=3
                new StructArgument(null, 2, null), // payload_size=2
            ]);

        // prefix=3bytes, payload=2bytes
        var data = new byte[] { 0x01, 0x02, 0x03, 0xAB, 0xCD };
        var result = _decoder.Decode(data, format);

        var entry = result.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        entry.Children[0].Should().BeOfType<DecodedBytes>().Which.Size.Should().Be(3);
        entry.Children[1].Should().BeOfType<DecodedBytes>().Which.Size.Should().Be(2);
    }

    [Fact]
    public void DefaultValues_Applied()
    {
        // 引数なしでデフォルト値(prefix_size=1, payload_size=2)が使われる
        var format = CreateDataBlockFormat(
            prefixSizeDefault: 1, payloadSizeDefault: 2,
            args: null);

        // prefix=1byte, payload=2bytes
        var data = new byte[] { 0xFF, 0x0A, 0x0B };
        var result = _decoder.Decode(data, format);

        var entry = result.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        entry.Children[0].Should().BeOfType<DecodedBytes>().Which.Size.Should().Be(1);
        entry.Children[1].Should().BeOfType<DecodedBytes>().Which.Size.Should().Be(2);
    }

    [Fact]
    public void PartialDefaults_MixArgs()
    {
        // prefix_size のみ指定(3)、payload_size はデフォルト(2)を使用
        var format = CreateDataBlockFormat(
            prefixSizeDefault: 1, payloadSizeDefault: 2,
            args: [new StructArgument("prefix_size", 3, null)]);

        // prefix=3bytes, payload=2bytes(default)
        var data = new byte[] { 0x01, 0x02, 0x03, 0xAA, 0xBB };
        var result = _decoder.Decode(data, format);

        var entry = result.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        entry.Children[0].Should().BeOfType<DecodedBytes>().Which.Size.Should().Be(3);
        entry.Children[1].Should().BeOfType<DecodedBytes>().Which.Size.Should().Be(2);
    }

    [Fact]
    public void ExpressionArg_EvaluatedAtRuntime()
    {
        // data_size フィールドの値を式引数として渡す
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
                        new FieldDefinition { Name = "data_size", Type = FieldType.UInt8 },
                        new FieldDefinition
                        {
                            Name = "entry",
                            Type = FieldType.Struct,
                            StructRef = "record",
                            StructArgs = [new StructArgument("size", null, ExpressionParser.Parse("{data_size}"))],
                        },
                    ],
                },
                ["record"] = new()
                {
                    Name = "record",
                    Parameters = [new TemplateParameter("size", null)],
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "data",
                            Type = FieldType.Bytes,
                            SizeExpression = ExpressionParser.Parse("{size}"),
                        },
                    ],
                },
            },
            RootStruct = "main",
        };

        // data_size=3, data=0x01,0x02,0x03
        var data = new byte[] { 0x03, 0x01, 0x02, 0x03 };
        var result = _decoder.Decode(data, format);

        var entry = result.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        entry.Children[0].Should().BeOfType<DecodedBytes>().Which.Size.Should().Be(3);
    }

    [Fact]
    public void MissingRequiredParam_Throws()
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
                            Name = "entry",
                            Type = FieldType.Struct,
                            StructRef = "record",
                        },
                    ],
                },
                ["record"] = new()
                {
                    Name = "record",
                    Parameters = [new TemplateParameter("size", null)], // required, no default
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "data",
                            Type = FieldType.Bytes,
                            SizeExpression = ExpressionParser.Parse("{size}"),
                        },
                    ],
                },
            },
            RootStruct = "main",
        };

        var data = new byte[] { 0x01, 0x02, 0x03 };
        var act = () => _decoder.Decode(data, format);

        act.Should().Throw<Exception>()
            .WithMessage("*Required template parameter*size*");
    }

    [Fact]
    public void WithRepeat_DecodesMultiple()
    {
        var format = CreateDataBlockFormat(
            prefixSizeDefault: 1, payloadSizeDefault: 2,
            args: [
                new StructArgument("prefix_size", 1, null),
                new StructArgument("payload_size", 2, null),
            ],
            repeatCount: 2);

        // entry[0]: prefix=0x01(1byte), payload=0xAA,0xBB(2bytes)
        // entry[1]: prefix=0x02(1byte), payload=0xCC,0xDD(2bytes)
        var data = new byte[] { 0x01, 0xAA, 0xBB, 0x02, 0xCC, 0xDD };
        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);

        var e0 = array.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        e0.Children[0].Should().BeOfType<DecodedBytes>().Which.Size.Should().Be(1);
        e0.Children[1].Should().BeOfType<DecodedBytes>().Which.Size.Should().Be(2);

        var e1 = array.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        e1.Children[0].Should().BeOfType<DecodedBytes>().Which.Size.Should().Be(1);
        e1.Children[1].Should().BeOfType<DecodedBytes>().Which.Size.Should().Be(2);
    }

    [Fact]
    public void ParamUsedInSizeExpression()
    {
        // パラメータがフィールドのsizeで使用される
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
                            Name = "entry",
                            Type = FieldType.Struct,
                            StructRef = "fixed_record",
                            StructArgs = [new StructArgument("record_size", 4, null)],
                        },
                    ],
                },
                ["fixed_record"] = new()
                {
                    Name = "fixed_record",
                    Parameters = [new TemplateParameter("record_size", 2)],
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "data",
                            Type = FieldType.Bytes,
                            SizeExpression = ExpressionParser.Parse("{record_size}"),
                        },
                    ],
                },
            },
            RootStruct = "main",
        };

        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var result = _decoder.Decode(data, format);

        var entry = result.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        entry.Children[0].Should().BeOfType<DecodedBytes>().Which.Size.Should().Be(4);
    }

    [Fact]
    public void VariableIsolation()
    {
        // テンプレートパラメータが親スコープに漏れないことを確認
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
                            Name = "block",
                            Type = FieldType.Struct,
                            StructRef = "sized",
                            StructArgs = [new StructArgument("sz", 2, null)],
                        },
                        new FieldDefinition { Name = "after", Type = FieldType.UInt8 },
                    ],
                },
                ["sized"] = new()
                {
                    Name = "sized",
                    Parameters = [new TemplateParameter("sz", null)],
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "data",
                            Type = FieldType.Bytes,
                            SizeExpression = ExpressionParser.Parse("{sz}"),
                        },
                    ],
                },
            },
            RootStruct = "main",
        };

        var data = new byte[] { 0xAA, 0xBB, 0xCC };
        var result = _decoder.Decode(data, format);

        result.Children.Should().HaveCount(2);
        var block = result.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        block.Children[0].Should().BeOfType<DecodedBytes>().Which.Size.Should().Be(2);

        var after = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        after.Value.Should().Be(0xCC);
    }

    [Fact]
    public void NonTemplate_UnchangedBehavior()
    {
        // パラメータなしstructは既存通り
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
                            Name = "inner",
                            Type = FieldType.Struct,
                            StructRef = "plain",
                        },
                    ],
                },
                ["plain"] = new()
                {
                    Name = "plain",
                    Fields =
                    [
                        new FieldDefinition { Name = "a", Type = FieldType.UInt8 },
                        new FieldDefinition { Name = "b", Type = FieldType.UInt8 },
                    ],
                },
            },
            RootStruct = "main",
        };

        var data = new byte[] { 0x0A, 0x0B };
        var result = _decoder.Decode(data, format);

        var inner = result.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        inner.Children[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0A);
        inner.Children[1].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0B);
    }

    // --- ヘルパー ---

    /// <summary>
    /// data_block(prefix_size, payload_size) テンプレート構造体を持つフォーマットを作成する。
    /// prefix と payload は bytes 型で、サイズはテンプレートパラメータで制御。
    /// </summary>
    private static FormatDefinition CreateDataBlockFormat(
        long prefixSizeDefault, long payloadSizeDefault,
        IReadOnlyList<StructArgument>? args,
        int? repeatCount = null)
    {
        var entryField = new FieldDefinition
        {
            Name = "entry",
            Type = FieldType.Struct,
            StructRef = "data_block",
            StructArgs = args,
            Repeat = repeatCount.HasValue
                ? new RepeatMode.Count(ExpressionParser.Parse($"{{{repeatCount.Value}}}"))
                : new RepeatMode.None(),
        };

        return new FormatDefinition
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
                    Fields = [entryField],
                },
                ["data_block"] = new()
                {
                    Name = "data_block",
                    Parameters =
                    [
                        new TemplateParameter("prefix_size", prefixSizeDefault),
                        new TemplateParameter("payload_size", payloadSizeDefault),
                    ],
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "prefix",
                            Type = FieldType.Bytes,
                            SizeExpression = ExpressionParser.Parse("{prefix_size}"),
                        },
                        new FieldDefinition
                        {
                            Name = "payload",
                            Type = FieldType.Bytes,
                            SizeExpression = ExpressionParser.Parse("{payload_size}"),
                        },
                    ],
                },
            },
            RootStruct = "main",
        };
    }
}
