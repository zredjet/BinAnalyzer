using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Validation;

public class SeekValidationTests
{
    [Fact]
    public void VAL011_SeekRestoreWithoutSeek_ReturnsError()
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
                        Name = "bad_field",
                        Type = FieldType.UInt8,
                        SeekRestore = true,
                    },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Errors.Should().Contain(d =>
            d.Code == "VAL011" && d.Message.Contains("bad_field"));
    }

    [Fact]
    public void SeekWithoutRestore_NoError()
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
                        Name = "field",
                        Type = FieldType.UInt8,
                        SeekExpression = ExpressionParser.Parse("{4}"),
                    },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Errors.Should().NotContain(d => d.Code == "VAL011");
    }

    [Fact]
    public void SeekWithRestore_NoError()
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
                        Name = "field",
                        Type = FieldType.UInt8,
                        SeekExpression = ExpressionParser.Parse("{4}"),
                        SeekRestore = true,
                    },
                ],
            },
        });

        var result = FormatValidator.Validate(format);

        result.Errors.Should().NotContain(d => d.Code == "VAL011");
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
