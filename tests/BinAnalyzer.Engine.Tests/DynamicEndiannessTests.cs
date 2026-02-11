using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class DynamicEndiannessTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void DynamicEndianness_EvaluatesToLittle()
    {
        var format = CreateFormatWithDynamicEndianness();

        // order='LE' (ascii bytes) + uint16 0x0100 in LE = 1
        var data = new byte[] { (byte)'L', (byte)'E', 0x01, 0x00 };
        var result = _decoder.Decode(data, format);

        var orderField = result.Children[0].Should().BeOfType<DecodedString>().Subject;
        orderField.Value.Should().Be("LE");

        var body = result.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var value = body.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        value.Value.Should().Be(1); // LE: 0x0001
    }

    [Fact]
    public void DynamicEndianness_EvaluatesToBig()
    {
        var format = CreateFormatWithDynamicEndianness();

        // order='BE' (ascii bytes) + uint16 0x0001 in BE = 1
        var data = new byte[] { (byte)'B', (byte)'E', 0x00, 0x01 };
        var result = _decoder.Decode(data, format);

        var orderField = result.Children[0].Should().BeOfType<DecodedString>().Subject;
        orderField.Value.Should().Be("BE");

        var body = result.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var value = body.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        value.Value.Should().Be(1); // BE: 0x0001
    }

    [Fact]
    public void DynamicEndianness_InheritsToChildStruct()
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
                        new FieldDefinition { Name = "order", Type = FieldType.Ascii, Size = 2 },
                        new FieldDefinition { Name = "body", Type = FieldType.Struct, StructRef = "body_struct" },
                    ],
                },
                ["body_struct"] = new()
                {
                    Name = "body_struct",
                    EndiannessExpression = ExpressionParser.Parse("{order == 'LE' ? 'little' : 'big'}"),
                    Fields =
                    [
                        new FieldDefinition { Name = "child", Type = FieldType.Struct, StructRef = "child_struct" },
                    ],
                },
                ["child_struct"] = new()
                {
                    Name = "child_struct",
                    Fields =
                    [
                        new FieldDefinition { Name = "value", Type = FieldType.UInt16 },
                    ],
                },
            },
            RootStruct = "main",
        };

        // order='LE' + uint16 0x0100 in LE = 1
        var data = new byte[] { (byte)'L', (byte)'E', 0x01, 0x00 };
        var result = _decoder.Decode(data, format);

        var body = result.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var child = body.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        var value = child.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        value.Value.Should().Be(1); // inherits LE from dynamic endianness
    }

    [Fact]
    public void DynamicEndianness_InvalidResult_Throws()
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
                        new FieldDefinition { Name = "order", Type = FieldType.Ascii, Size = 2 },
                        new FieldDefinition { Name = "body", Type = FieldType.Struct, StructRef = "body_struct" },
                    ],
                },
                ["body_struct"] = new()
                {
                    Name = "body_struct",
                    EndiannessExpression = ExpressionParser.Parse("{order}"),
                    Fields =
                    [
                        new FieldDefinition { Name = "value", Type = FieldType.UInt16 },
                    ],
                },
            },
            RootStruct = "main",
        };

        // order='XX' → invalid endianness
        var data = new byte[] { (byte)'X', (byte)'X', 0x00, 0x01 };
        var act = () => _decoder.Decode(data, format);
        act.Should().Throw<Exception>().WithMessage("*invalid value*");
    }

    private static FormatDefinition CreateFormatWithDynamicEndianness()
    {
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
                    Fields =
                    [
                        new FieldDefinition { Name = "order", Type = FieldType.Ascii, Size = 2 },
                        new FieldDefinition { Name = "body", Type = FieldType.Struct, StructRef = "body_struct" },
                    ],
                },
                ["body_struct"] = new()
                {
                    Name = "body_struct",
                    EndiannessExpression = ExpressionParser.Parse("{order == 'LE' ? 'little' : 'big'}"),
                    Fields =
                    [
                        new FieldDefinition { Name = "value", Type = FieldType.UInt16 },
                    ],
                },
            },
            RootStruct = "main",
        };
    }
}
