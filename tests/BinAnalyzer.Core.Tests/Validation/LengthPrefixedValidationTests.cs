using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Validation;

public class LengthPrefixedValidationTests
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

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public void VAL014_InvalidPrefixSize_ReportsError(int prefixSize)
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
                        Name = "blocks",
                        Type = FieldType.Bytes,
                        Repeat = new RepeatMode.LengthPrefixed(prefixSize),
                    },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Errors.Should().Contain(d => d.Code == "VAL014" && d.FieldName == "blocks");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void VAL014_ValidPrefixSize_NoError(int prefixSize)
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
                        Name = "blocks",
                        Type = FieldType.Bytes,
                        Repeat = new RepeatMode.LengthPrefixed(prefixSize),
                    },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Errors.Should().NotContain(d => d.Code == "VAL014");
    }

    [Fact]
    public void VAL111_NonBytesType_ReportsWarning()
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
                        Name = "blocks",
                        Type = FieldType.UInt8,
                        Repeat = new RepeatMode.LengthPrefixed(1),
                    },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().Contain(d => d.Code == "VAL111" && d.FieldName == "blocks");
    }

    [Fact]
    public void VAL111_BytesType_NoWarning()
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
                        Name = "blocks",
                        Type = FieldType.Bytes,
                        Repeat = new RepeatMode.LengthPrefixed(1),
                    },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().NotContain(d => d.Code == "VAL111");
    }

    [Fact]
    public void VAL007_LengthPrefixed_BytesWithoutSize_NoError()
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
                        Name = "blocks",
                        Type = FieldType.Bytes,
                        Repeat = new RepeatMode.LengthPrefixed(1),
                        // No Size, SizeExpression, or SizeRemaining
                    },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Errors.Should().NotContain(d => d.Code == "VAL007");
    }
}
