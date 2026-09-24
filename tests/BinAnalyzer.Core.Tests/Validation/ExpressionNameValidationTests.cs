using System.Text.RegularExpressions;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Validation;

/// <summary>VAL124（式の未定義の名前）/ VAL125（未知の関数）。REQ-189。</summary>
public class ExpressionNameValidationTests
{
    private static Expression E(string text) => ExpressionParser.Parse(text);

    private static FieldDefinition U8(string name) => new() { Name = name, Type = FieldType.UInt8 };

    /// <summary>root（先頭に <c>compression</c>、続けて <paramref name="fields"/>）と、追加の struct からなる定義。</summary>
    private static FormatDefinition CreateFormat(IEnumerable<FieldDefinition> fields, params StructDefinition[] others)
    {
        var structs = new Dictionary<string, StructDefinition>
        {
            ["root"] = new() { Name = "root", Fields = [U8("compression"), .. fields] },
        };
        foreach (var s in others)
            structs[s.Name] = s;
        return new FormatDefinition
        {
            Name = "test",
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = structs,
            RootStruct = "root",
        };
    }

    private static IReadOnlyList<ValidationDiagnostic> Diagnostics(FormatDefinition format, string code) =>
        FormatValidator.Validate(format).Diagnostics.Where(d => d.Code == code).ToList();

    // --- VAL124: true / false ---

    [Fact]
    public void VAL124_FalseLiteral_WarnsWithOneZeroHint()
    {
        var format = CreateFormat([
            new FieldDefinition
            {
                Name = "masks", Type = FieldType.UInt8,
                Condition = E("{compression == 40 ? compression == 3 : false}"),
                SourceFile = "t.bdef.yaml", SourceLine = 12,
            },
        ]);

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeTrue();
        var d = result.Warnings.Should().ContainSingle().Subject;
        d.Code.Should().Be("VAL124");
        d.StructName.Should().Be("root");
        d.FieldName.Should().Be("masks");
        d.Message.Should().Contain("'false'").And.Contain(" if ").And.Contain("1（真）/ 0（偽）");
        d.Message.Should().Contain("{compression == 40 ? compression == 3 : false}");
        d.Location.Should().Be("t.bdef.yaml:12");
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("FALSE")]
    public void VAL124_TrueFalseAnyCase_WarnsWithOneZeroHint(string word)
    {
        var format = CreateFormat([new FieldDefinition { Name = "v", Type = FieldType.Virtual, ValueExpression = E($"{{{word}}}") }]);

        Diagnostics(format, "VAL124").Should().ContainSingle()
            .Which.Message.Should().Contain($"'{word}'").And.Contain("1（真）/ 0（偽）").And.NotContain("もしかして");
    }

    [Fact]
    public void VAL124_FieldNamedFalse_NoWarning()
    {
        var format = CreateFormat([U8("false"), new FieldDefinition { Name = "v", Type = FieldType.Virtual, ValueExpression = E("{false}") }]);

        Diagnostics(format, "VAL124").Should().BeEmpty();
    }

    // --- VAL124: 書き間違い ---

    [Fact]
    public void VAL124_Typo_SuggestsClosestName()
    {
        var format = CreateFormat([new FieldDefinition { Name = "data", Type = FieldType.Bytes, SizeExpression = E("{compresion * 2}") }]);

        var d = Diagnostics(format, "VAL124").Should().ContainSingle().Subject;
        d.Message.Should().Contain("フィールド 'data' の size の式 '{compresion * 2}' が未定義の名前 'compresion' を参照しています");
        d.Message.Should().Contain("（もしかして 'compression'?）");
    }

    [Fact]
    public void VAL124_NothingClose_NoSuggestion()
    {
        var format = CreateFormat([new FieldDefinition { Name = "data", Type = FieldType.Bytes, SizeExpression = E("{totally_unrelated}") }]);

        Diagnostics(format, "VAL124").Should().ContainSingle()
            .Which.Message.Should().Contain("どのフィールド・bitfield のエントリ・テンプレートのパラメータにもありません").And.NotContain("もしかして");
    }

    // --- VAL124: 定義のどこかにある名前には出ない（昇格による参照） ---

    [Fact]
    public void VAL124_NameDefinedInAnotherStruct_NoWarning()
    {
        var header = new StructDefinition { Name = "header", Fields = [U8("length")] };
        var format = CreateFormat(
            [
                new FieldDefinition { Name = "hdr", Type = FieldType.Struct, StructRef = "header" },
                new FieldDefinition { Name = "body", Type = FieldType.Bytes, SizeExpression = E("{length}") },
            ],
            header);

        Diagnostics(format, "VAL124").Should().BeEmpty();
    }

    [Fact]
    public void VAL124_BitfieldEntryName_NoWarning()
    {
        var format = CreateFormat([
            new FieldDefinition
            {
                Name = "flags", Type = FieldType.Bitfield, Size = 1,
                BitfieldEntries = [new BitfieldEntry { Name = "has_crc", BitHigh = 0, BitLow = 0 }],
            },
            new FieldDefinition { Name = "crc", Type = FieldType.UInt32, Condition = E("{has_crc}") },
        ]);

        Diagnostics(format, "VAL124").Should().BeEmpty();
    }

    [Fact]
    public void VAL124_TemplateParameter_NoWarning()
    {
        var record = new StructDefinition
        {
            Name = "record",
            Parameters = [new TemplateParameter("width", null)],
            Fields = [new FieldDefinition { Name = "value", Type = FieldType.Bytes, SizeExpression = E("{width}") }],
        };
        var format = CreateFormat(
            [new FieldDefinition { Name = "r", Type = FieldType.Struct, StructRef = "record", StructArgs = [new StructArgument("width", 4, null)] }],
            record);

        Diagnostics(format, "VAL124").Should().BeEmpty();
    }

    [Fact]
    public void VAL124_SpecialVariables_NoWarning()
    {
        var format = CreateFormat([
            U8("offsets"),
            new FieldDefinition
            {
                Name = "items", Type = FieldType.UInt8,
                Repeat = new RepeatMode.While(E("{remaining > 0}")),
                Condition = E("{_index > 0 ? _prev.value < 10 : 1}"),
                SeekExpression = E("{offsets[_index]}"),
            },
        ]);

        Diagnostics(format, "VAL124").Should().BeEmpty();
    }

    [Fact]
    public void VAL124_StateReference_NoWarning()
    {
        var format = CreateFormat([new FieldDefinition { Name = "v", Type = FieldType.Virtual, ValueExpression = E("{@counter + 1}") }]);

        Diagnostics(format, "VAL124").Should().BeEmpty();
    }

    [Fact]
    public void VAL124_MemberName_IsNotChecked()
    {
        // メンバー名は検査しない（先頭の名前だけ）
        var format = CreateFormat([new FieldDefinition { Name = "v", Type = FieldType.Virtual, ValueExpression = E("{compression.no_such_member}") }]);

        Diagnostics(format, "VAL124").Should().BeEmpty();
    }

    // --- VAL124: 検査する式の場所 ---

    public static TheoryData<string, Func<Expression, FieldDefinition>> ExpressionKeys() => new()
    {
        { "size", e => new FieldDefinition { Name = "f", Type = FieldType.Bytes, SizeExpression = e } },
        { "element_size", e => new FieldDefinition { Name = "f", Type = FieldType.Bytes, Repeat = new RepeatMode.Count(E("{2}")), ElementSizeExpression = e } },
        { "if", e => new FieldDefinition { Name = "f", Type = FieldType.UInt8, Condition = e } },
        { "value", e => new FieldDefinition { Name = "f", Type = FieldType.Virtual, ValueExpression = e } },
        { "seek", e => new FieldDefinition { Name = "f", Type = FieldType.UInt8, SeekExpression = e } },
        { "seek_base", e => new FieldDefinition { Name = "f", Type = FieldType.UInt8, SeekExpression = E("{0}"), SeekBaseExpression = e } },
        { "validate", e => new FieldDefinition { Name = "f", Type = FieldType.UInt8, ValidationExpression = e } },
        { "switch_on", e => new FieldDefinition { Name = "f", Type = FieldType.Switch, SwitchOn = e, SwitchDefault = "root" } },
        { "cases", e => new FieldDefinition { Name = "f", Type = FieldType.Switch, SwitchOn = E("{compression}"), SwitchCases = [new SwitchCase(e, "root")], SwitchDefault = "root" } },
        { "repeat_count", e => new FieldDefinition { Name = "f", Type = FieldType.UInt8, Repeat = new RepeatMode.Count(e) } },
        { "repeat_until", e => new FieldDefinition { Name = "f", Type = FieldType.UInt8, Repeat = new RepeatMode.UntilValue(e) } },
        { "repeat_while", e => new FieldDefinition { Name = "f", Type = FieldType.UInt8, Repeat = new RepeatMode.While(e) } },
        { "repeat_max", e => new FieldDefinition { Name = "f", Type = FieldType.UInt8, Repeat = new RepeatMode.UntilEof(), RepeatMax = e } },
        { "repeat_error_limit", e => new FieldDefinition { Name = "f", Type = FieldType.UInt8, Repeat = new RepeatMode.UntilEof(), RepeatErrorLimit = e } },
        { "state_if", e => new FieldDefinition { Name = "f", Type = FieldType.UInt8, State = "s", StateIf = e } },
        { "struct の引数", e => new FieldDefinition { Name = "f", Type = FieldType.Struct, StructRef = "root", StructArgs = [new StructArgument("n", null, e)] } },
        { "checksum の range の offset", e => new FieldDefinition { Name = "f", Type = FieldType.UInt32, Checksum = new ChecksumSpec { Algorithm = "crc32", FieldNames = [], Range = new ChecksumRange { OffsetExpression = e, SizeExpression = E("{4}") } } } },
        { "checksum の range の size", e => new FieldDefinition { Name = "f", Type = FieldType.UInt32, Checksum = new ChecksumSpec { Algorithm = "crc32", FieldNames = [], Ranges = [new ChecksumRange { OffsetExpression = E("{0}"), SizeExpression = e }] } } },
    };

    [Theory]
    [MemberData(nameof(ExpressionKeys))]
    public void VAL124_EachExpressionKey_IsChecked(string key, Func<Expression, FieldDefinition> field)
    {
        var format = CreateFormat([field(E("{undefined_name}"))]);

        Diagnostics(format, "VAL124").Should().ContainSingle()
            .Which.Message.Should().StartWith($"フィールド 'f' の {key} の式 '{{undefined_name}}'");
    }

    [Fact]
    public void VAL124_StructEndianness_IsChecked()
    {
        var body = new StructDefinition
        {
            Name = "body",
            EndiannessExpression = E("{byte_ordr == 0x4949 ? 'little' : 'big'}"),
            Fields = [U8("byte_order")],
        };
        var format = CreateFormat([new FieldDefinition { Name = "b", Type = FieldType.Struct, StructRef = "body" }], body);

        var d = Diagnostics(format, "VAL124").Should().ContainSingle().Subject;
        d.StructName.Should().Be("body");
        d.FieldName.Should().BeNull();
        d.Message.Should().StartWith("構造体 'body' の endianness の式").And.Contain("（もしかして 'byte_order'?）");
    }

    [Theory]
    [InlineData("{arr_missing[0]}", "arr_missing")]              // 配列名
    [InlineData("{compression[idx_missing]}", "idx_missing")]    // 添字
    [InlineData("{head_missing.size}", "head_missing")]          // メンバーアクセスの先頭
    [InlineData("{compression.items[i_missing].size}", "i_missing")] // 式の結果への添字
    [InlineData("{len(arg_missing)}", "arg_missing")]            // 関数の引数
    [InlineData("{cond_missing ? 1 : 0}", "cond_missing")]       // 三項演算子の条件
    [InlineData("{1 ? then_missing : 0}", "then_missing")]       // 真の枝
    [InlineData("{1 ? 0 : else_missing}", "else_missing")]       // 偽の枝
    [InlineData("{not neg_missing}", "neg_missing")]             // 単項演算子
    [InlineData("{(1 + (2 * deep_missing))}", "deep_missing")]   // 括弧・二項演算子
    public void VAL124_NestedPositions_AreChecked(string text, string missing)
    {
        var format = CreateFormat([new FieldDefinition { Name = "v", Type = FieldType.Virtual, ValueExpression = E(text) }]);

        Diagnostics(format, "VAL124").Should().ContainSingle()
            .Which.Message.Should().Contain($"未定義の名前 '{missing}'");
    }

    [Fact]
    public void VAL124_SameNameTwiceInOneExpression_ReportedOnce()
    {
        var format = CreateFormat([
            new FieldDefinition { Name = "v", Type = FieldType.Virtual, ValueExpression = E("{foo + foo * bar}"), Condition = E("{foo}") },
        ]);

        // value の式で foo と bar が 1 件ずつ、別の式（if）の foo がもう 1 件
        Diagnostics(format, "VAL124").Select(d => Regex.Match(d.Message, @"^(.+) の式 '.*' が未定義の名前 '(\w+)'") is var m ? $"{m.Groups[1]} {m.Groups[2]}" : "")
            .Should().BeEquivalentTo("フィールド 'v' の value foo", "フィールド 'v' の value bar", "フィールド 'v' の if foo");
    }

    // --- VAL125: 未知の関数 ---

    [Fact]
    public void VAL125_UnknownFunction_WarnsWithSuggestion()
    {
        var format = CreateFormat([new FieldDefinition { Name = "v", Type = FieldType.Virtual, ValueExpression = E("{lenn(compression)}") }]);

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeTrue();
        var d = result.Warnings.Should().ContainSingle().Subject;
        d.Code.Should().Be("VAL125");
        d.FieldName.Should().Be("v");
        d.Message.Should().Contain("組み込み関数に無い関数 'lenn'").And.Contain("（もしかして 'len'?）");
    }

    [Fact]
    public void VAL125_BuiltinFunctions_NoWarning()
    {
        var calls = string.Join(" + ", BuiltinFunctions.Names.Select(n => $"{n}(compression)"));
        var format = CreateFormat([new FieldDefinition { Name = "v", Type = FieldType.Virtual, ValueExpression = E($"{{{calls}}}") }]);

        FormatValidator.Validate(format).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void VAL125_FunctionNameIsNotAVariable()
    {
        // 関数名は名前の集合と別。フィールドと同名でも関数として無ければ VAL125、名前としての VAL124 は出ない
        var format = CreateFormat([new FieldDefinition { Name = "v", Type = FieldType.Virtual, ValueExpression = E("{compression(1)}") }]);

        var result = FormatValidator.Validate(format);

        result.Warnings.Select(d => d.Code).Should().Equal("VAL125");
    }
}
