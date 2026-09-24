using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

/// <summary>式の未定義の名前（REQ-189）: YAML から読んだ定義で、VAL124 が式を書いたフィールドの行に出ること。</summary>
public sealed class ExpressionNameIntegrationTests
{
    /// <summary>REQ-188 で見つかった BMP のバグの形（<c>: false</c>）。40 バイトのヘッダでは枝を通らないのでデコードは通っていた。</summary>
    private const string BmpFalseLiteral = """
        name: t
        root: header
        structs:
          header:
            - name: header_size
              type: uint32
            - name: compression
              type: uint32
            - name: color_masks
              type: bytes
              size: "12"
              if: "{header_size == 40 ? compression == 3 or compression == 6 : false}"
        """;

    [Fact]
    public void BmpFalseLiteral_IsReportedAtFieldLine()
    {
        var format = new YamlFormatLoader().LoadFromString(BmpFalseLiteral);

        var result = FormatValidator.Validate(format);

        result.IsValid.Should().BeTrue();
        var d = result.Warnings.Should().ContainSingle().Subject;
        d.Code.Should().Be("VAL124");
        d.FieldName.Should().Be("color_masks");
        d.SourceLine.Should().Be(9);
        d.Message.Should().Contain("'false'").And.Contain("1（真）/ 0（偽）");
    }

    [Fact]
    public void BmpFalseLiteral_FixedWithZero_HasNoDiagnostics()
    {
        var format = new YamlFormatLoader().LoadFromString(BmpFalseLiteral.Replace(": false}", ": 0}"));

        FormatValidator.Validate(format).Diagnostics.Should().BeEmpty();
    }
}
