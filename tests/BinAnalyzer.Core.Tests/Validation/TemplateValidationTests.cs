using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Validation;

public class TemplateValidationTests
{
    [Fact]
    public void VAL116_RequiredParamsWithoutArgs_ReportsWarning()
    {
        var format = new FormatDefinition
        {
            Name = "test",
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["root"] = new()
                {
                    Name = "root",
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "entry",
                            Type = FieldType.Struct,
                            StructRef = "template",
                            // StructArgs is null — missing required args
                        },
                    ],
                },
                ["template"] = new()
                {
                    Name = "template",
                    Parameters = [new TemplateParameter("size", null)], // required
                    Fields =
                    [
                        new FieldDefinition { Name = "dummy", Type = FieldType.UInt8 },
                    ],
                },
            },
            RootStruct = "root",
        };

        var result = FormatValidator.Validate(format);

        result.Diagnostics.Should().Contain(d =>
            d.Code == "VAL116" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void VAL116_AllDefaultsNoArgs_NoWarning()
    {
        var format = new FormatDefinition
        {
            Name = "test",
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["root"] = new()
                {
                    Name = "root",
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "entry",
                            Type = FieldType.Struct,
                            StructRef = "template",
                            // StructArgs is null — but all params have defaults
                        },
                    ],
                },
                ["template"] = new()
                {
                    Name = "template",
                    Parameters = [new TemplateParameter("size", 4)], // has default
                    Fields =
                    [
                        new FieldDefinition { Name = "dummy", Type = FieldType.UInt8 },
                    ],
                },
            },
            RootStruct = "root",
        };

        var result = FormatValidator.Validate(format);

        result.Diagnostics.Should().NotContain(d => d.Code == "VAL116");
    }
}
