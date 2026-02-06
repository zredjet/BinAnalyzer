using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class ConditionalFieldTests
{
    [Fact]
    public void Decode_ConditionTrue_FieldIsIncluded()
    {
        // version=2 → extra_flags フィールドが含まれる
        var format = CreateConditionalFormat();
        var data = new byte[] { 0x02, 0x00, 0x0A, 0xFF };
        // version=2, extra_flags=0x000A, trailing=0xFF

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        result.Children.Should().HaveCount(3);
        result.Children[0].Name.Should().Be("version");
        result.Children[1].Name.Should().Be("extra_flags");
        result.Children[2].Name.Should().Be("trailing");

        var extraFlags = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        extraFlags.Value.Should().Be(0x0A);
    }

    [Fact]
    public void Decode_ConditionFalse_FieldIsSkipped()
    {
        // version=1 → extra_flags フィールドがスキップされる
        var format = CreateConditionalFormat();
        var data = new byte[] { 0x01, 0xFF };
        // version=1, (extra_flags スキップ), trailing=0xFF

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        result.Children.Should().HaveCount(2);
        result.Children[0].Name.Should().Be("version");
        result.Children[1].Name.Should().Be("trailing");

        var trailing = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        trailing.Value.Should().Be(0xFF);
    }

    [Fact]
    public void Decode_SkippedField_BytesNotConsumed()
    {
        // スキップされたフィールドのバイトが読み取られないことを確認
        var format = CreateConditionalFormat();
        var data = new byte[] { 0x01, 0x42 };
        // version=1, trailing=0x42（extra_flagsの2バイトは読み取られない）

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var trailing = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        trailing.Value.Should().Be(0x42);
        trailing.Offset.Should().Be(1); // version(1) の直後
    }

    [Fact]
    public void Decode_MultipleConditionalFields_IndependentEvaluation()
    {
        var format = new FormatDefinition
        {
            Name = "test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["root"] = new()
                {
                    Name = "root",
                    Fields =
                    [
                        new FieldDefinition { Name = "flags", Type = FieldType.UInt8 },
                        new FieldDefinition
                        {
                            Name = "field_a", Type = FieldType.UInt8,
                            Condition = ExpressionParser.Parse("{flags == 1}"),
                        },
                        new FieldDefinition
                        {
                            Name = "field_b", Type = FieldType.UInt8,
                            Condition = ExpressionParser.Parse("{flags == 2}"),
                        },
                        new FieldDefinition { Name = "end", Type = FieldType.UInt8 },
                    ],
                },
            },
            RootStruct = "root",
        };

        // flags=1 → field_a あり, field_b なし
        var data = new byte[] { 0x01, 0xAA, 0xBB };
        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        result.Children.Should().HaveCount(3);
        result.Children[0].Name.Should().Be("flags");
        result.Children[1].Name.Should().Be("field_a");
        result.Children[2].Name.Should().Be("end");

        result.Children[1].Should().BeOfType<DecodedInteger>().Subject.Value.Should().Be(0xAA);
        result.Children[2].Should().BeOfType<DecodedInteger>().Subject.Value.Should().Be(0xBB);
    }

    private static FormatDefinition CreateConditionalFormat()
    {
        return new FormatDefinition
        {
            Name = "test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["root"] = new()
                {
                    Name = "root",
                    Fields =
                    [
                        new FieldDefinition { Name = "version", Type = FieldType.UInt8 },
                        new FieldDefinition
                        {
                            Name = "extra_flags",
                            Type = FieldType.UInt16,
                            Condition = ExpressionParser.Parse("{version >= 2}"),
                        },
                        new FieldDefinition { Name = "trailing", Type = FieldType.UInt8 },
                    ],
                },
            },
            RootStruct = "root",
        };
    }
}
