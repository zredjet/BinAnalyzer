using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Validation;

public class UnknownKeyValidationTests
{
    private static FormatDefinition CreateFormat(params DslUnknownKey[] unknownKeys) =>
        new()
        {
            Name = "test",
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["root"] = new()
                {
                    Name = "root",
                    Fields = [new FieldDefinition { Name = "magic", Type = FieldType.UInt8, SourceFile = "t.bdef.yaml", SourceLine = 5 }],
                    SourceFile = "t.bdef.yaml",
                    SourceLine = 4,
                },
            },
            RootStruct = "root",
            UnknownKeys = unknownKeys,
        };

    // --- VAL123: DSL の未知キー ---

    [Fact]
    public void VAL123_UnknownKey_ReportsWarningAtKeyLineWithSuggestion()
    {
        var format = CreateFormat(
            new DslUnknownKey("expect", "struct 'root' のフィールド 'magic'", "root", "magic", "expected", "t.bdef.yaml", 7));

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeTrue();
        var warning = result.Warnings.Should().ContainSingle(d => d.Code == "VAL123").Subject;
        warning.Message.Should().Be("struct 'root' のフィールド 'magic' の未知のキー 'expect' は無視されます（もしかして 'expected'?）");
        warning.StructName.Should().Be("root");
        warning.FieldName.Should().Be("magic");
        // フィールドの行（5）ではなくキーの行
        warning.SourceLine.Should().Be(7);
        warning.MessageWithLocation.Should().EndWith("(t.bdef.yaml:7)");
    }

    [Fact]
    public void VAL123_WithoutSuggestion_OmitsHint()
    {
        var format = CreateFormat(new DslUnknownKey("zzz", "トップレベル", null, null, null, null, 2));

        var warning = FormatValidator.Validate(format).Warnings.Should().ContainSingle(d => d.Code == "VAL123").Subject;

        warning.Message.Should().Be("トップレベルの未知のキー 'zzz' は無視されます");
        warning.Location.Should().Be("line 2");
    }

    [Fact]
    public void NoUnknownKeys_NoVAL123()
    {
        FormatValidator.Validate(CreateFormat()).Warnings.Should().NotContain(d => d.Code == "VAL123");
    }
}
