using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Validation;

/// <summary>
/// REQ-142: state 関連バリデーション
/// </summary>
public class StateValidationTests
{
    [Fact]
    public void StateIf_WithoutState_ReturnsError()
    {
        var format = CreateFormat(
            new FieldDefinition
            {
                Name = "val",
                Type = FieldType.UInt8,
                StateIf = ExpressionParser.Parse("{val > 0}"),
                // State は未指定
            });

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(d => d.Code == "VAL017" && d.FieldName == "val");
    }

    [Fact]
    public void StateDefault_WithoutState_ReturnsWarning()
    {
        var format = CreateFormat(
            new FieldDefinition
            {
                Name = "val",
                Type = FieldType.UInt8,
                StateDefault = 42,
                // State は未指定
            });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().Contain(d => d.Code == "VAL117" && d.FieldName == "val");
    }

    [Fact]
    public void State_WithStateIf_NoError()
    {
        var format = CreateFormat(
            new FieldDefinition
            {
                Name = "val",
                Type = FieldType.UInt8,
                State = "my_state",
                StateIf = ExpressionParser.Parse("{val > 0}"),
            });

        var result = FormatValidator.Validate(format);

        result.Errors.Should().NotContain(d => d.Code == "VAL017");
    }

    [Fact]
    public void State_WithStateDefault_NoWarning()
    {
        var format = CreateFormat(
            new FieldDefinition
            {
                Name = "val",
                Type = FieldType.UInt8,
                State = "my_state",
                StateDefault = 0,
            });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().NotContain(d => d.Code == "VAL117");
    }

    // --- ヘルパー ---

    private static FormatDefinition CreateFormat(params FieldDefinition[] fields)
    {
        return new FormatDefinition
        {
            Name = "test",
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["root"] = new()
                {
                    Name = "root",
                    Fields = fields.ToList(),
                },
            },
            RootStruct = "root",
        };
    }
}
