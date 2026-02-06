using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Validation;

public class FormatValidatorTests
{
    // --- ヘルパー ---

    private static FormatDefinition CreateFormat(
        Dictionary<string, StructDefinition>? structs = null,
        Dictionary<string, EnumDefinition>? enums = null,
        Dictionary<string, FlagsDefinition>? flags = null,
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
            Enums = enums ?? new Dictionary<string, EnumDefinition>(),
            Flags = flags ?? new Dictionary<string, FlagsDefinition>(),
            Structs = structs,
            RootStruct = rootStruct,
        };
    }

    private static StructDefinition Struct(string name, params FieldDefinition[] fields) =>
        new() { Name = name, Fields = fields };

    private static FieldDefinition Field(string name, FieldType type) =>
        new() { Name = name, Type = type };

    // --- VAL001: struct型フィールドの StructRef が未指定 ---

    [Fact]
    public void VAL001_StructFieldWithoutStructRef_ReportsError()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "nested", Type = FieldType.Struct }),
        });

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(d => d.Code == "VAL001" && d.FieldName == "nested");
    }

    // --- VAL002: StructRef が存在しないstruct名を参照 ---

    [Fact]
    public void VAL002_StructRefToUndefinedStruct_ReportsError()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "nested", Type = FieldType.Struct, StructRef = "nonexistent" }),
        });

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(d => d.Code == "VAL002" && d.FieldName == "nested");
    }

    // --- VAL003: switch case の参照先structが未定義 ---

    [Fact]
    public void VAL003_SwitchCaseRefToUndefinedStruct_ReportsError()
    {
        var switchOn = ExpressionParser.Parse("{type}");
        var caseCondition = ExpressionParser.Parse("{'A'}");

        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "type", Type = FieldType.Ascii, Size = 1 },
                new FieldDefinition
                {
                    Name = "data",
                    Type = FieldType.Switch,
                    Size = 4,
                    SwitchOn = switchOn,
                    SwitchCases = [new SwitchCase(caseCondition, "nonexistent")],
                    SwitchDefault = "root",
                }),
        });

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(d => d.Code == "VAL003" && d.FieldName == "data");
    }

    // --- VAL004: switch default の参照先structが未定義 ---

    [Fact]
    public void VAL004_SwitchDefaultRefToUndefinedStruct_ReportsError()
    {
        var switchOn = ExpressionParser.Parse("{type}");

        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "type", Type = FieldType.Ascii, Size = 1 },
                new FieldDefinition
                {
                    Name = "data",
                    Type = FieldType.Switch,
                    Size = 4,
                    SwitchOn = switchOn,
                    SwitchDefault = "nonexistent",
                }),
        });

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(d => d.Code == "VAL004" && d.FieldName == "data");
    }

    // --- VAL005: switch型フィールドに switch_on が未指定 ---

    [Fact]
    public void VAL005_SwitchFieldWithoutSwitchOn_ReportsError()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition
                {
                    Name = "data",
                    Type = FieldType.Switch,
                    Size = 4,
                    SwitchDefault = "root",
                }),
        });

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(d => d.Code == "VAL005" && d.FieldName == "data");
    }

    // --- VAL006: switch型フィールドに cases も default もない ---

    [Fact]
    public void VAL006_SwitchFieldWithoutCasesAndDefault_ReportsError()
    {
        var switchOn = ExpressionParser.Parse("{type}");

        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "type", Type = FieldType.Ascii, Size = 1 },
                new FieldDefinition
                {
                    Name = "data",
                    Type = FieldType.Switch,
                    Size = 4,
                    SwitchOn = switchOn,
                }),
        });

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(d => d.Code == "VAL006" && d.FieldName == "data");
    }

    // --- VAL007: サイズ指定が必要な型でサイズ未指定 ---

    [Theory]
    [InlineData(FieldType.Bytes)]
    [InlineData(FieldType.Ascii)]
    [InlineData(FieldType.Utf8)]
    public void VAL007_SizedTypeWithoutSize_ReportsError(FieldType type)
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "field1", Type = type }),
        });

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(d => d.Code == "VAL007" && d.FieldName == "field1");
    }

    [Fact]
    public void VAL007_SizedTypeWithFixedSize_NoError()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "field1", Type = FieldType.Bytes, Size = 8 }),
        });

        var result = FormatValidator.Validate(format);

        result.Errors.Should().NotContain(d => d.Code == "VAL007");
    }

    [Fact]
    public void VAL007_SizedTypeWithSizeExpression_NoError()
    {
        var expr = ExpressionParser.Parse("{length}");

        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "length", Type = FieldType.UInt32 },
                new FieldDefinition { Name = "data", Type = FieldType.Bytes, SizeExpression = expr }),
        });

        var result = FormatValidator.Validate(format);

        result.Errors.Should().NotContain(d => d.Code == "VAL007");
    }

    [Fact]
    public void VAL007_SizedTypeWithSizeRemaining_NoError()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "data", Type = FieldType.Bytes, SizeRemaining = true }),
        });

        var result = FormatValidator.Validate(format);

        result.Errors.Should().NotContain(d => d.Code == "VAL007");
    }

    // --- VAL101: EnumRef が存在しないenum名を参照 ---

    [Fact]
    public void VAL101_EnumRefToUndefinedEnum_ReportsWarning()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "value", Type = FieldType.UInt8, EnumRef = "nonexistent" }),
        });

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(d => d.Code == "VAL101" && d.FieldName == "value");
    }

    // --- VAL102: FlagsRef が存在しないflags名を参照 ---

    [Fact]
    public void VAL102_FlagsRefToUndefinedFlags_ReportsWarning()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "type", Type = FieldType.Ascii, Size = 4, FlagsRef = "nonexistent" }),
        });

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(d => d.Code == "VAL102" && d.FieldName == "type");
    }

    // --- VAL103: EnumRef が整数型以外のフィールドに指定 ---

    [Fact]
    public void VAL103_EnumRefOnNonIntegerField_ReportsWarning()
    {
        var format = CreateFormat(
            new Dictionary<string, StructDefinition>
            {
                ["root"] = Struct("root",
                    new FieldDefinition { Name = "text", Type = FieldType.Utf8, Size = 4, EnumRef = "my_enum" }),
            },
            enums: new Dictionary<string, EnumDefinition>
            {
                ["my_enum"] = new()
                {
                    Name = "my_enum",
                    Entries = [new EnumEntry(0, "zero")],
                },
            });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().Contain(d => d.Code == "VAL103" && d.FieldName == "text");
    }

    [Fact]
    public void VAL103_EnumRefOnIntegerField_NoWarning()
    {
        var format = CreateFormat(
            new Dictionary<string, StructDefinition>
            {
                ["root"] = Struct("root",
                    new FieldDefinition { Name = "value", Type = FieldType.UInt8, EnumRef = "my_enum" }),
            },
            enums: new Dictionary<string, EnumDefinition>
            {
                ["my_enum"] = new()
                {
                    Name = "my_enum",
                    Entries = [new EnumEntry(0, "zero")],
                },
            });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().NotContain(d => d.Code == "VAL103");
    }

    // --- VAL104: FlagsRef がascii型以外のフィールドに指定 ---

    [Fact]
    public void VAL104_FlagsRefOnNonAsciiField_ReportsWarning()
    {
        var format = CreateFormat(
            new Dictionary<string, StructDefinition>
            {
                ["root"] = Struct("root",
                    new FieldDefinition { Name = "value", Type = FieldType.UInt32, FlagsRef = "my_flags" }),
            },
            flags: new Dictionary<string, FlagsDefinition>
            {
                ["my_flags"] = new()
                {
                    Name = "my_flags",
                    BitSize = 32,
                    Fields = [new FlagFieldDefinition("flag1", 0, 1)],
                },
            });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().Contain(d => d.Code == "VAL104" && d.FieldName == "value");
    }

    // --- VAL105: switch に default がない ---

    [Fact]
    public void VAL105_SwitchWithoutDefault_ReportsWarning()
    {
        var switchOn = ExpressionParser.Parse("{type}");
        var caseCondition = ExpressionParser.Parse("{'A'}");

        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "type", Type = FieldType.Ascii, Size = 1 },
                new FieldDefinition
                {
                    Name = "data",
                    Type = FieldType.Switch,
                    Size = 4,
                    SwitchOn = switchOn,
                    SwitchCases = [new SwitchCase(caseCondition, "root")],
                }),
            // root はcaseの参照先としても使用
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().Contain(d => d.Code == "VAL105" && d.FieldName == "data");
    }

    // --- VAL106: struct型でないフィールドに StructRef が指定 ---

    [Fact]
    public void VAL106_StructRefOnNonStructField_ReportsWarning()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "value", Type = FieldType.UInt8, StructRef = "root" }),
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().Contain(d => d.Code == "VAL106" && d.FieldName == "value");
    }

    // --- VAL107: 未使用のenum定義 ---

    [Fact]
    public void VAL107_UnusedEnum_ReportsWarning()
    {
        var format = CreateFormat(
            new Dictionary<string, StructDefinition>
            {
                ["root"] = Struct("root", Field("dummy", FieldType.UInt8)),
            },
            enums: new Dictionary<string, EnumDefinition>
            {
                ["unused_enum"] = new()
                {
                    Name = "unused_enum",
                    Entries = [new EnumEntry(0, "zero")],
                },
            });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().Contain(d => d.Code == "VAL107" && d.Message.Contains("unused_enum"));
    }

    [Fact]
    public void VAL107_UsedEnum_NoWarning()
    {
        var format = CreateFormat(
            new Dictionary<string, StructDefinition>
            {
                ["root"] = Struct("root",
                    new FieldDefinition { Name = "value", Type = FieldType.UInt8, EnumRef = "my_enum" }),
            },
            enums: new Dictionary<string, EnumDefinition>
            {
                ["my_enum"] = new()
                {
                    Name = "my_enum",
                    Entries = [new EnumEntry(0, "zero")],
                },
            });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().NotContain(d => d.Code == "VAL107");
    }

    // --- VAL108: 未使用のflags定義 ---

    [Fact]
    public void VAL108_UnusedFlags_ReportsWarning()
    {
        var format = CreateFormat(
            new Dictionary<string, StructDefinition>
            {
                ["root"] = Struct("root", Field("dummy", FieldType.UInt8)),
            },
            flags: new Dictionary<string, FlagsDefinition>
            {
                ["unused_flags"] = new()
                {
                    Name = "unused_flags",
                    BitSize = 8,
                    Fields = [new FlagFieldDefinition("flag1", 0, 1)],
                },
            });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().Contain(d => d.Code == "VAL108" && d.Message.Contains("unused_flags"));
    }

    // --- VAL109: rootから到達不可能なstruct ---

    [Fact]
    public void VAL109_UnreachableStruct_ReportsWarning()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root", Field("dummy", FieldType.UInt8)),
            ["orphan"] = Struct("orphan", Field("x", FieldType.UInt8)),
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().Contain(d => d.Code == "VAL109" && d.Message.Contains("orphan"));
    }

    [Fact]
    public void VAL109_ReachableViaStructRef_NoWarning()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "child", Type = FieldType.Struct, StructRef = "child_struct" }),
            ["child_struct"] = Struct("child_struct", Field("x", FieldType.UInt8)),
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().NotContain(d => d.Code == "VAL109");
    }

    [Fact]
    public void VAL109_ReachableViaSwitchCase_NoWarning()
    {
        var switchOn = ExpressionParser.Parse("{type}");
        var caseCondition = ExpressionParser.Parse("{'A'}");

        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "type", Type = FieldType.Ascii, Size = 1 },
                new FieldDefinition
                {
                    Name = "data",
                    Type = FieldType.Switch,
                    Size = 4,
                    SwitchOn = switchOn,
                    SwitchCases = [new SwitchCase(caseCondition, "case_a")],
                    SwitchDefault = "fallback",
                }),
            ["case_a"] = Struct("case_a", Field("x", FieldType.UInt8)),
            ["fallback"] = Struct("fallback", Field("y", FieldType.UInt8)),
        });

        var result = FormatValidator.Validate(format);

        result.Warnings.Should().NotContain(d => d.Code == "VAL109");
    }

    // --- 正常系: png.bdef.yaml 相当の定義 ---

    [Fact]
    public void ValidFormat_NoErrors()
    {
        var switchOn = ExpressionParser.Parse("{type}");

        var format = new FormatDefinition
        {
            Name = "PNG",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>
            {
                ["color_type"] = new()
                {
                    Name = "color_type",
                    Entries = [new EnumEntry(0, "grayscale"), new EnumEntry(2, "truecolor")],
                },
            },
            Flags = new Dictionary<string, FlagsDefinition>
            {
                ["chunk_type_flags"] = new()
                {
                    Name = "chunk_type_flags",
                    BitSize = 32,
                    Fields = [new FlagFieldDefinition("ancillary", 5, 1, "yes", "no")],
                },
            },
            Structs = new Dictionary<string, StructDefinition>
            {
                ["png"] = Struct("png",
                    new FieldDefinition
                    {
                        Name = "signature",
                        Type = FieldType.Bytes,
                        Size = 8,
                        Expected = [0x89, 0x50, 0x4E, 0x47],
                    },
                    new FieldDefinition
                    {
                        Name = "chunks",
                        Type = FieldType.Struct,
                        StructRef = "chunk",
                        Repeat = new RepeatMode.UntilEof(),
                    }),
                ["chunk"] = Struct("chunk",
                    new FieldDefinition { Name = "length", Type = FieldType.UInt32 },
                    new FieldDefinition { Name = "type", Type = FieldType.Ascii, Size = 4, FlagsRef = "chunk_type_flags" },
                    new FieldDefinition
                    {
                        Name = "data",
                        Type = FieldType.Switch,
                        SizeExpression = ExpressionParser.Parse("{length}"),
                        SwitchOn = switchOn,
                        SwitchCases = [new SwitchCase(ExpressionParser.Parse("{'IHDR'}"), "ihdr")],
                        SwitchDefault = "raw_data",
                    },
                    new FieldDefinition { Name = "crc", Type = FieldType.UInt32 }),
                ["ihdr"] = Struct("ihdr",
                    new FieldDefinition { Name = "width", Type = FieldType.UInt32 },
                    new FieldDefinition { Name = "height", Type = FieldType.UInt32 },
                    new FieldDefinition { Name = "color_type", Type = FieldType.UInt8, EnumRef = "color_type" }),
                ["raw_data"] = Struct("raw_data",
                    new FieldDefinition { Name = "data", Type = FieldType.Bytes, SizeRemaining = true }),
            },
            RootStruct = "png",
        };

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    // --- エラーメッセージにstruct名・フィールド名が含まれること ---

    [Fact]
    public void ErrorMessages_ContainStructAndFieldNames()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "broken_field", Type = FieldType.Struct }),
        });

        var result = FormatValidator.Validate(format);

        var error = result.Errors.First(d => d.Code == "VAL001");
        error.StructName.Should().Be("root");
        error.FieldName.Should().Be("broken_field");
        error.Message.Should().Contain("broken_field");
    }
}
