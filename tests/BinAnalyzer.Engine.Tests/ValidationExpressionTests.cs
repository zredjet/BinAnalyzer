using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class ValidationExpressionTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void Validate_Passed_SetsValidationPassed()
    {
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "magic",
                Type = FieldType.UInt16,
                ValidationExpression = ExpressionParser.Parse("{magic == 42}"),
            });

        var data = new byte[] { 0x00, 0x2A }; // BE: 42
        var result = _decoder.Decode(data, format);

        var node = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        node.Value.Should().Be(42);
        node.Validation.Should().NotBeNull();
        node.Validation!.Passed.Should().BeTrue();
        node.Validation.Expression.Should().Be("{magic == 42}");
    }

    [Fact]
    public void Validate_Failed_SetsValidationFailed()
    {
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "magic",
                Type = FieldType.UInt16,
                ValidationExpression = ExpressionParser.Parse("{magic == 42}"),
            });

        var data = new byte[] { 0x00, 0x01 }; // BE: 1
        var result = _decoder.Decode(data, format);

        var node = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        node.Value.Should().Be(1);
        node.Validation.Should().NotBeNull();
        node.Validation!.Passed.Should().BeFalse();
    }

    [Fact]
    public void Validate_OnStringField()
    {
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "sig",
                Type = FieldType.Ascii,
                Size = 2,
                ValidationExpression = ExpressionParser.Parse("{sig == 'II'}"),
            });

        var data = "II"u8.ToArray();
        var result = _decoder.Decode(data, format);

        var node = result.Children[0].Should().BeOfType<DecodedString>().Subject;
        node.Validation.Should().NotBeNull();
        node.Validation!.Passed.Should().BeTrue();
    }

    [Fact]
    public void Validate_ReferencesOtherFields()
    {
        var format = CreateFormat("main",
            new FieldDefinition { Name = "a", Type = FieldType.UInt8 },
            new FieldDefinition
            {
                Name = "b",
                Type = FieldType.UInt8,
                ValidationExpression = ExpressionParser.Parse("{b > a}"),
            });

        var data = new byte[] { 0x05, 0x0A }; // a=5, b=10
        var result = _decoder.Decode(data, format);

        var bNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        bNode.Validation.Should().NotBeNull();
        bNode.Validation!.Passed.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithoutValidation_NoValidationInfo()
    {
        var format = CreateFormat("main",
            new FieldDefinition { Name = "value", Type = FieldType.UInt8 });

        var data = new byte[] { 0x42 };
        var result = _decoder.Decode(data, format);

        var node = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        node.Validation.Should().BeNull();
    }

    [Fact]
    public void Validate_PreservedThroughPaddingFlag()
    {
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "reserved",
                Type = FieldType.UInt8,
                IsPadding = true,
                ValidationExpression = ExpressionParser.Parse("{reserved == 0}"),
            });

        var data = new byte[] { 0x00 };
        var result = _decoder.Decode(data, format);

        var node = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        node.IsPadding.Should().BeTrue();
        node.Validation.Should().NotBeNull();
        node.Validation!.Passed.Should().BeTrue();
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
