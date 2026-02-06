using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Validation;

public class VirtualFieldValidationTests
{
    [Fact]
    public void VAL010_VirtualFieldWithoutValue_ReportsError()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = new()
            {
                Name = "root",
                Fields =
                [
                    new FieldDefinition { Name = "calc", Type = FieldType.Virtual },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Errors.Should().Contain(d =>
            d.Code == "VAL010" && d.Message.Contains("calc"));
    }

    [Fact]
    public void VAL010_VirtualFieldWithValue_NoError()
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
                        Name = "calc",
                        Type = FieldType.Virtual,
                        ValueExpression = ExpressionParser.Parse("{42}"),
                    },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Errors.Should().NotContain(d => d.Code == "VAL010");
    }

    [Fact]
    public void VirtualField_DoesNotRequireSize()
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
                        Name = "calc",
                        Type = FieldType.Virtual,
                        ValueExpression = ExpressionParser.Parse("{42}"),
                    },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Errors.Should().NotContain(d => d.Code == "VAL007");
    }

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
}
