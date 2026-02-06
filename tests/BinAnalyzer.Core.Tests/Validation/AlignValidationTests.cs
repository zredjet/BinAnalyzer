using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Validation;

public class AlignValidationTests
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

    // --- VAL008: フィールドの align 値が正の整数であること ---

    [Fact]
    public void VAL008_FieldAlignZero_ReportsError()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = new()
            {
                Name = "root",
                Fields =
                [
                    new FieldDefinition { Name = "field1", Type = FieldType.UInt8, Align = 0 },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(d => d.Code == "VAL008" && d.FieldName == "field1");
    }

    [Fact]
    public void VAL008_FieldAlignNegative_ReportsError()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = new()
            {
                Name = "root",
                Fields =
                [
                    new FieldDefinition { Name = "field1", Type = FieldType.UInt8, Align = -4 },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(d => d.Code == "VAL008" && d.FieldName == "field1");
    }

    [Fact]
    public void VAL008_FieldAlignPositive_NoError()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = new()
            {
                Name = "root",
                Fields =
                [
                    new FieldDefinition { Name = "field1", Type = FieldType.UInt8, Align = 4 },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Errors.Should().NotContain(d => d.Code == "VAL008");
    }

    [Fact]
    public void VAL008_FieldAlignNull_NoError()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = new()
            {
                Name = "root",
                Fields =
                [
                    new FieldDefinition { Name = "field1", Type = FieldType.UInt8 },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Errors.Should().NotContain(d => d.Code == "VAL008");
    }

    // --- VAL009: 構造体の align 値が正の整数であること ---

    [Fact]
    public void VAL009_StructAlignZero_ReportsError()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = new()
            {
                Name = "root",
                Align = 0,
                Fields =
                [
                    new FieldDefinition { Name = "field1", Type = FieldType.UInt8 },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(d => d.Code == "VAL009");
    }

    [Fact]
    public void VAL009_StructAlignNegative_ReportsError()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = new()
            {
                Name = "root",
                Align = -2,
                Fields =
                [
                    new FieldDefinition { Name = "field1", Type = FieldType.UInt8 },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(d => d.Code == "VAL009");
    }

    [Fact]
    public void VAL009_StructAlignPositive_NoError()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = new()
            {
                Name = "root",
                Align = 16,
                Fields =
                [
                    new FieldDefinition { Name = "field1", Type = FieldType.UInt8 },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Errors.Should().NotContain(d => d.Code == "VAL009");
    }
}
