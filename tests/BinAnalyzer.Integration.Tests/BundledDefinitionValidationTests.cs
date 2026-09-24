using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

/// <summary>
/// 同梱の全定義で検証の警告・エラーが 0 件であること（REQ-186）。GUI は診断があれば表示する（REQ-185）ので、
/// 同梱定義で何か出れば本物の問題として扱う。
/// </summary>
public sealed class BundledDefinitionValidationTests
{
    private static readonly string FormatsDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats"));

    public static TheoryData<string> BundledFormats()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(FormatsDir, "*.bdef.yaml").Order())
            data.Add(Path.GetFileName(path));
        return data;
    }

    [Theory]
    [MemberData(nameof(BundledFormats))]
    public void BundledFormat_HasNoDiagnostics(string file)
    {
        var format = new YamlFormatLoader().Load(Path.Combine(FormatsDir, file));

        var diagnostics = FormatValidator.Validate(format).Diagnostics;

        diagnostics.Select(d => $"{d.Code}: {d.MessageWithLocation}").Should().BeEmpty();
    }
}
