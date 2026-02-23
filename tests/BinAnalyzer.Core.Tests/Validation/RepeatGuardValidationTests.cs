using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Validation;

public class RepeatGuardValidationTests
{
    // --- VAL120: repeat_max on non-repeat field ---

    [Fact]
    public void VAL120_RepeatMaxOnNonRepeatField_ReportsWarning()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition
                {
                    Name = "value",
                    Type = FieldType.UInt8,
                    RepeatMax = ExpressionParser.Parse("{100}"),
                    // Repeat is default (None)
                }),
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().Contain(d => d.Code == "VAL120" && d.FieldName == "value");
    }

    [Fact]
    public void VAL120_RepeatMaxOnRepeatField_NoWarning()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.UInt8,
                    Repeat = new RepeatMode.UntilEof(),
                    RepeatMax = ExpressionParser.Parse("{100}"),
                }),
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().NotContain(d => d.Code == "VAL120");
    }

    // --- VAL121: repeat_error_limit on non-repeat field ---

    [Fact]
    public void VAL121_RepeatErrorLimitOnNonRepeatField_ReportsWarning()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition
                {
                    Name = "value",
                    Type = FieldType.UInt8,
                    RepeatErrorLimit = ExpressionParser.Parse("{5}"),
                }),
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().Contain(d => d.Code == "VAL121" && d.FieldName == "value");
    }

    [Fact]
    public void VAL121_RepeatErrorLimitOnRepeatField_NoWarning()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.UInt8,
                    Repeat = new RepeatMode.UntilEof(),
                    RepeatErrorLimit = ExpressionParser.Parse("{5}"),
                }),
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().NotContain(d => d.Code == "VAL121");
    }

    // --- Both guards on non-repeat field ---

    [Fact]
    public void BothGuardsOnNonRepeatField_ReportsBothWarnings()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition
                {
                    Name = "value",
                    Type = FieldType.UInt8,
                    RepeatMax = ExpressionParser.Parse("{100}"),
                    RepeatErrorLimit = ExpressionParser.Parse("{3}"),
                }),
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().Contain(d => d.Code == "VAL120" && d.FieldName == "value");
        result.Warnings.Should().Contain(d => d.Code == "VAL121" && d.FieldName == "value");
    }

    // --- No guard: no warnings ---

    [Fact]
    public void NoGuard_NoWarnings()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "value", Type = FieldType.UInt8 }),
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().NotContain(d => d.Code == "VAL120");
        result.Warnings.Should().NotContain(d => d.Code == "VAL121");
    }

    // --- ヘルパー ---

    private static FormatDefinition CreateFormat(
        Dictionary<string, StructDefinition>? structs = null,
        string rootStruct = "root")
    {
        structs ??= new Dictionary<string, StructDefinition>
        {
            ["root"] = new()
            {
                Name = "root",
                Fields = [new FieldDefinition { Name = "dummy", Type = FieldType.UInt8 }],
            },
        };

        return new FormatDefinition
        {
            Name = "test",
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = structs,
            RootStruct = rootStruct,
        };
    }

    private static StructDefinition Struct(string name, params FieldDefinition[] fields) =>
        new() { Name = name, Fields = fields };
}
