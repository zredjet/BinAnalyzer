using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Validation;

public class ElementSizeValidationTests
{
    private static FormatDefinition CreateFormat(
        Dictionary<string, StructDefinition> structs) =>
        new()
        {
            Name = "test",
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = structs,
            RootStruct = "root",
        };

    [Fact]
    public void VAL110_NonRepeatField_WithElementSize_ReportsWarning()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = new()
            {
                Name = "root",
                Fields =
                [
                    new FieldDefinition
                    {
                        Name = "field1",
                        Type = FieldType.UInt8,
                        ElementSize = 4,
                    },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().Contain(d => d.Code == "VAL110" && d.FieldName == "field1");
    }

    [Fact]
    public void VAL110_NonRepeatField_WithElementSizeExpression_ReportsWarning()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = new()
            {
                Name = "root",
                Fields =
                [
                    new FieldDefinition
                    {
                        Name = "field1",
                        Type = FieldType.UInt8,
                        ElementSizeExpression = ExpressionParser.Parse("{size}"),
                    },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().Contain(d => d.Code == "VAL110" && d.FieldName == "field1");
    }

    [Fact]
    public void VAL110_RepeatCountField_WithElementSize_NoWarning()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = new()
            {
                Name = "root",
                Fields =
                [
                    new FieldDefinition
                    {
                        Name = "items",
                        Type = FieldType.UInt8,
                        Repeat = new RepeatMode.Count(ExpressionParser.Parse("{3}")),
                        ElementSize = 4,
                    },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().NotContain(d => d.Code == "VAL110");
    }

    [Fact]
    public void VAL110_RepeatEofField_WithElementSize_NoWarning()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = new()
            {
                Name = "root",
                Fields =
                [
                    new FieldDefinition
                    {
                        Name = "items",
                        Type = FieldType.UInt8,
                        Repeat = new RepeatMode.UntilEof(),
                        ElementSize = 8,
                    },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().NotContain(d => d.Code == "VAL110");
    }

    [Fact]
    public void VAL110_RepeatUntilField_WithElementSize_NoWarning()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = new()
            {
                Name = "root",
                Fields =
                [
                    new FieldDefinition
                    {
                        Name = "items",
                        Type = FieldType.UInt8,
                        Repeat = new RepeatMode.UntilValue(ExpressionParser.Parse("{items == 0}")),
                        ElementSize = 4,
                    },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().NotContain(d => d.Code == "VAL110");
    }

    [Fact]
    public void VAL110_NoElementSize_NoWarning()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = new()
            {
                Name = "root",
                Fields =
                [
                    new FieldDefinition
                    {
                        Name = "field1",
                        Type = FieldType.UInt8,
                    },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().NotContain(d => d.Code == "VAL110");
    }
}
