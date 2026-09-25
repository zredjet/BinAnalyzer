using BinAnalyzer.Core.Validation;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

/// <summary>REQ-172: struct とフィールドの定義元（ファイルと行）を IR に持たせる。</summary>
public class SourceLocationTests
{
    private static readonly string RepoFormats = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats"));

    private readonly YamlFormatLoader _loader = new();

    /// <summary>ファイル中で <paramref name="text"/> を含む最初の行（1 始まり）。<paramref name="after"/> より後ろから探す。</summary>
    private static int LineOf(string[] lines, string text, int after = 0)
    {
        for (var i = after; i < lines.Length; i++)
            if (lines[i].Contains(text, StringComparison.Ordinal))
                return i + 1;
        throw new InvalidOperationException($"'{text}' not found");
    }

    [Fact]
    public void Png_IhdrWidth_SourceLine_MatchesTheFile()
    {
        var path = Path.Combine(RepoFormats, "png.bdef.yaml");
        var lines = File.ReadAllLines(path);
        var format = _loader.Load(path);

        var ihdr = format.Structs["ihdr"];
        var ihdrLine = LineOf(lines, "  ihdr:");
        ihdr.SourceLine.Should().Be(ihdrLine, "struct の行はキーの行");
        ihdr.SourceFile.Should().Be(path);

        var width = ihdr.Fields.Single(f => f.Name == "width");
        width.SourceLine.Should().Be(LineOf(lines, "- name: width", after: ihdrLine));
        width.SourceFile.Should().Be(path);

        // 全フィールドの行が単調増加で、ファイルの範囲内
        foreach (var s in format.Structs.Values)
        {
            var fieldLines = s.Fields.Select(f => f.SourceLine!.Value).ToList();
            fieldLines.Should().BeInAscendingOrder(s.Name);
            fieldLines.Should().OnlyContain(l => l > s.SourceLine && l <= lines.Length, s.Name);
        }
    }

    [Fact]
    public void DuplicateFieldNames_GetDistinctLines()
    {
        const string yaml = """
            name: T
            root: main
            structs:
              main:
                - name: v
                  type: uint8
                - name: v
                  type: uint16
                  if: "{v == 1}"
              other:
                fields:
                  - name: v
                    type: uint32
            """;
        var format = _loader.LoadFromString(yaml);
        var main = format.Structs["main"];
        main.SourceLine.Should().Be(4);
        main.Fields[0].SourceLine.Should().Be(5);
        main.Fields[1].SourceLine.Should().Be(7);
        var other = format.Structs["other"];
        other.SourceLine.Should().Be(10, "新形式（fields: を持つマッピング）でもキーの行");
        other.Fields[0].SourceLine.Should().Be(12);
        main.SourceFile.Should().BeNull("文字列から読んだのでファイルは無い");
        main.Fields[0].SourceFile.Should().BeNull();
    }

    [Fact]
    public void ImportedStruct_SourceFile_IsTheImportedFile()
    {
        var path = Path.Combine(RepoFormats, "wav.bdef.yaml");
        var format = _loader.Load(path);

        format.Structs["wav"].SourceFile.Should().Be(path);
        var imported = format.Structs["raw_data"];
        imported.SourceFile.Should().EndWith(Path.Combine("common", "riff.bdef.yaml"));
        imported.SourceFile.Should().NotBe(path);
        var riffLines = File.ReadAllLines(imported.SourceFile!);
        imported.SourceLine.Should().Be(LineOf(riffLines, "  raw_data:"));
        imported.Fields.Should().OnlyContain(f => f.SourceFile == imported.SourceFile && f.SourceLine > imported.SourceLine);
    }

    [Fact]
    public void ValidatorDiagnostics_CarryFileAndLine()
    {
        var path = Path.Combine(RepoFormats, "common", "isobmff.bdef.yaml");
        var lines = File.ReadAllLines(path);
        var result = FormatValidator.Validate(_loader.Load(path));

        // 単独では iso_box が未定義（container_box の children と dref_box の entries が参照する）
        var undefined = result.Errors.Where(d => d.Code == "VAL002").OrderBy(d => d.SourceLine).ToList();
        undefined.Should().HaveCount(2);
        var error = undefined[0];
        error.SourceFile.Should().Be(path);
        error.SourceLine.Should().Be(LineOf(lines, "- name: children"));
        error.Location.Should().Be($"isobmff.bdef.yaml:{error.SourceLine}");
        error.MessageWithLocation.Should().EndWith($"(isobmff.bdef.yaml:{error.SourceLine})");

        var unreachable = result.Warnings.First(d => d.Code == "VAL109" && d.StructName == "ftyp_box");
        unreachable.SourceLine.Should().Be(LineOf(lines, "  ftyp_box:"), "struct 単位の診断は struct の行");
    }

    [Fact]
    public void DiagnosticsFromStringLoad_HaveLineOnly()
    {
        const string yaml = """
            name: T
            root: main
            structs:
              main:
                - name: body
                  type: struct
                  struct: missing
            """;
        var result = FormatValidator.Validate(_loader.LoadFromString(yaml));
        var error = result.Errors.Single(d => d.Code == "VAL002");
        error.SourceLine.Should().Be(5);
        error.SourceFile.Should().BeNull();
        error.Location.Should().Be("line 5");
    }
}
