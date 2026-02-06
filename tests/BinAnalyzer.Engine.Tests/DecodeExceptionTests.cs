using BinAnalyzer.Core;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class DecodeExceptionTests
{
    [Fact]
    public void DataInsufficient_ThrowsDecodeExceptionWithOffset()
    {
        // 2バイトしかないデータに対してuint32を読もうとする
        var format = CreateFormat("root", [
            new FieldDefinition { Name = "value", Type = FieldType.UInt32 },
        ]);
        var data = new byte[] { 0x01, 0x02 };

        var act = () => new BinaryDecoder().Decode(data, format);
        var ex = act.Should().Throw<DecodeException>().Subject.First();

        ex.FieldPath.Should().Contain("value");
        ex.Offset.Should().Be(0);
        ex.FieldType.Should().Be("UInt32");
    }

    [Fact]
    public void UndefinedVariable_ThrowsDecodeExceptionWithPath()
    {
        // 未定義の変数をsize式で参照
        var format = CreateFormat("root", [
            new FieldDefinition
            {
                Name = "data",
                Type = FieldType.Bytes,
                SizeExpression = ExpressionParser.Parse("{undefined_var}"),
            },
        ]);
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        var act = () => new BinaryDecoder().Decode(data, format);
        var ex = act.Should().Throw<DecodeException>().Subject.First();

        ex.FieldPath.Should().Contain("data");
    }

    [Fact]
    public void NestedStruct_ThrowsDecodeExceptionWithNestedPath()
    {
        // ネストしたstruct内でエラーが発生した場合、パスにネスト情報が含まれる
        var innerStruct = new StructDefinition { Name = "inner", Fields = [
            new FieldDefinition { Name = "big_field", Type = FieldType.UInt64 },
        ] };
        var rootStruct = new StructDefinition { Name = "root", Fields = [
            new FieldDefinition { Name = "header", Type = FieldType.Struct, StructRef = "inner" },
        ] };
        var format = new FormatDefinition
        {
            Name = "test",
            Endianness = Endianness.Big,
            RootStruct = "root",
            Structs = new Dictionary<string, StructDefinition>
            {
                ["root"] = rootStruct,
                ["inner"] = innerStruct,
            },
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
        };
        var data = new byte[] { 0x01, 0x02 }; // 8バイト必要だが2バイトしかない

        var act = () => new BinaryDecoder().Decode(data, format);
        var ex = act.Should().Throw<DecodeException>().Subject.First();

        ex.FieldPath.Should().Contain("header");
        ex.FieldPath.Should().Contain("big_field");
    }

    [Fact]
    public void DecodeException_FormatMessage_ContainsAllFields()
    {
        var ex = new DecodeException(
            "テストエラー", 0x10, "chunks[0].data",
            "Switch", "switch_onの前にフィールドが定義されているか確認してください");

        var msg = ex.FormatMessage();
        msg.Should().Contain("デコードエラー: テストエラー");
        msg.Should().Contain("chunks[0].data");
        msg.Should().Contain("0x00000010");
        msg.Should().Contain("Switch");
        msg.Should().Contain("ヒント:");
    }

    [Fact]
    public void DecodeException_FormatMessage_WithoutOptionalFields()
    {
        var ex = new DecodeException("エラー", 0, "field");

        var msg = ex.FormatMessage();
        msg.Should().Contain("デコードエラー: エラー");
        msg.Should().Contain("field");
        msg.Should().NotContain("フィールド型:");
        msg.Should().NotContain("ヒント:");
    }

    private static FormatDefinition CreateFormat(string rootName, List<FieldDefinition> fields)
    {
        return new FormatDefinition
        {
            Name = "test",
            Endianness = Endianness.Big,
            RootStruct = rootName,
            Structs = new Dictionary<string, StructDefinition>
            {
                [rootName] = new StructDefinition { Name = rootName, Fields = fields },
            },
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
        };
    }
}
