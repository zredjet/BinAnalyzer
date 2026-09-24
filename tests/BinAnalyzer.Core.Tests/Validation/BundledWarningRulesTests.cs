using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Validation;

/// <summary>REQ-186: 同梱定義で誤検知になっていた VAL103 / VAL107 / VAL109 の判定。</summary>
public class BundledWarningRulesTests
{
    private static readonly Dictionary<string, EnumDefinition> Kinds = new()
    {
        ["kind"] = new() { Name = "kind", Entries = [new EnumEntry(0, "zero")] },
    };

    private static FormatDefinition Format(Dictionary<string, StructDefinition> structs, Dictionary<string, EnumDefinition>? enums = null) =>
        new()
        {
            Name = "test",
            Enums = enums ?? new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = structs,
            RootStruct = "root",
        };

    // --- VAL107: bitfield のエントリからの参照も使用に数える ---

    [Fact]
    public void VAL107_EnumReferencedOnlyByBitfieldEntry_NoWarning()
    {
        var format = Format(new()
        {
            ["root"] = new()
            {
                Name = "root",
                Fields =
                [
                    new FieldDefinition
                    {
                        Name = "bits", Type = FieldType.Bitfield, Size = 1,
                        BitfieldEntries = [new BitfieldEntry { Name = "k", BitHigh = 3, BitLow = 0, EnumRef = "kind" }],
                    },
                ],
            },
        }, Kinds);

        FormatValidator.Validate(format).Warnings.Should().NotContain(d => d.Code == "VAL107");
    }

    [Fact]
    public void VAL107_BitfieldWithoutEnum_StillReportsUnusedEnum()
    {
        var format = Format(new()
        {
            ["root"] = new()
            {
                Name = "root",
                Fields =
                [
                    new FieldDefinition
                    {
                        Name = "bits", Type = FieldType.Bitfield, Size = 1,
                        BitfieldEntries = [new BitfieldEntry { Name = "k", BitHigh = 3, BitLow = 0 }],
                    },
                ],
            },
        }, Kinds);

        FormatValidator.Validate(format).Warnings.Should().Contain(d => d.Code == "VAL107" && d.Message.Contains("kind"));
    }

    // --- VAL109: 本ファイルで定義した struct だけを検査する ---

    [Fact]
    public void VAL109_UnreachableImportedStruct_NoWarning()
    {
        var format = Format(new()
        {
            ["root"] = new() { Name = "root", Fields = [new FieldDefinition { Name = "x", Type = FieldType.UInt8 }], SourceFile = "/f/main.bdef.yaml", SourceLine = 3 },
            ["_lib_common"] = new() { Name = "_lib_common", Fields = [], SourceFile = "/f/common/lib.bdef.yaml", SourceLine = 4 },
        });

        FormatValidator.Validate(format).Warnings.Should().NotContain(d => d.Code == "VAL109");
    }

    [Fact]
    public void VAL109_UnreachableStructInMainFile_StillReported()
    {
        var format = Format(new()
        {
            ["root"] = new() { Name = "root", Fields = [new FieldDefinition { Name = "x", Type = FieldType.UInt8 }], SourceFile = "/f/main.bdef.yaml", SourceLine = 3 },
            ["orphan"] = new() { Name = "orphan", Fields = [], SourceFile = "/f/main.bdef.yaml", SourceLine = 9 },
            ["_lib_common"] = new() { Name = "_lib_common", Fields = [], SourceFile = "/f/common/lib.bdef.yaml", SourceLine = 4 },
        });

        FormatValidator.Validate(format).Warnings.Where(d => d.Code == "VAL109").Should().ContainSingle()
            .Which.StructName.Should().Be("orphan");
    }

    // --- VAL103: virtual への enum は許す ---

    [Fact]
    public void VAL103_EnumOnVirtual_NoWarning()
    {
        var format = Format(new()
        {
            ["root"] = new()
            {
                Name = "root",
                Fields =
                [
                    new FieldDefinition { Name = "raw", Type = FieldType.UInt8 },
                    new FieldDefinition
                    {
                        Name = "k", Type = FieldType.Virtual, EnumRef = "kind",
                        ValueExpression = global::BinAnalyzer.Core.Expressions.ExpressionParser.Parse("{raw & 3}"),
                    },
                ],
            },
        }, Kinds);

        FormatValidator.Validate(format).Warnings.Should().NotContain(d => d.Code == "VAL103" || d.Code == "VAL107");
    }

    [Fact]
    public void VAL103_EnumOnBytes_StillReported()
    {
        var format = Format(new()
        {
            ["root"] = new() { Name = "root", Fields = [new FieldDefinition { Name = "b", Type = FieldType.Bytes, Size = 2, EnumRef = "kind" }] },
        }, Kinds);

        FormatValidator.Validate(format).Warnings.Should().Contain(d => d.Code == "VAL103" && d.FieldName == "b");
    }
}
