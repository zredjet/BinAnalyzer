using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

/// <summary>
/// GUI / Web が拡張子からフォーマットを選ぶ一覧（wwwroot/formats/format-list.json）と formats/*.bdef.yaml の一致（REQ-147）。
/// cbor / msgpack が一覧から漏れ、拡張子で自動選択されない状態だったため固定する。
/// </summary>
public sealed class FormatListConsistencyTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private sealed record Entry(string Name, string File, string[] Extensions);

    private static List<Entry> Load(string json) =>
        JsonDocument.Parse(json).RootElement.EnumerateArray()
            .Select(e => new Entry(
                e.GetProperty("name").GetString()!,
                e.GetProperty("file").GetString()!,
                e.GetProperty("extensions").EnumerateArray().Select(x => x.GetString()!).ToArray()))
            .ToList();

    /// <summary>一覧と定義ファイルの不一致・拡張子の重複・書式の誤り。</summary>
    private static List<string> Check(List<Entry> entries, IReadOnlyCollection<string> formatFiles)
    {
        var problems = new List<string>();
        var listed = entries.Select(e => e.File).ToList();
        foreach (var file in formatFiles.Where(f => !listed.Contains(f)).Order())
            problems.Add($"formats/{file} が format-list.json にありません（GUI / Web で拡張子から選ばれない）");
        foreach (var file in listed.Where(f => !formatFiles.Contains(f)).Order())
            problems.Add($"format-list.json の {file} は formats/ にありません");
        foreach (var dup in listed.GroupBy(f => f).Where(g => g.Count() > 1))
            problems.Add($"format-list.json に {dup.Key} が重複しています");
        foreach (var ext in entries.SelectMany(e => e.Extensions.Select(x => (Ext: x.ToLowerInvariant(), e.File)))
                     .GroupBy(x => x.Ext).Where(g => g.Count() > 1))
            problems.Add($"拡張子 {ext.Key} が複数のフォーマットにあります: {string.Join(", ", ext.Select(x => x.File))}");
        foreach (var (ext, file) in entries.SelectMany(e => e.Extensions.Select(x => (x, e.File))).Where(x => !x.x.StartsWith('.')))
            problems.Add($"{file} の拡張子 '{ext}' は '.' で始まっていません");
        return problems;
    }

    private static List<string> FormatFiles() =>
        Directory.GetFiles(Path.Combine(RepoRoot, "formats"), "*.bdef.yaml").Select(p => Path.GetFileName(p)!).ToList();

    private static string ListJson() =>
        File.ReadAllText(Path.Combine(RepoRoot, "src", "BinAnalyzer.Web", "wwwroot", "formats", "format-list.json"));

    [Fact]
    public void FormatList_MatchesFormatsDirectory()
    {
        Check(Load(ListJson()), FormatFiles()).Should().BeEmpty();
    }

    [Fact]
    public void FormatList_DetectsMissingEntryAndDuplicateExtension()
    {
        var entries = Load(ListJson());
        entries.RemoveAll(e => e.File == "cbor.bdef.yaml");
        entries.Add(new Entry("Dup", "png.bdef.yaml", [".jpg"]));

        var problems = Check(entries, FormatFiles());

        problems.Should().Contain(p => p.Contains("cbor.bdef.yaml が format-list.json にありません"));
        problems.Should().Contain(p => p.Contains("png.bdef.yaml が重複"));
        problems.Should().Contain(p => p.Contains("拡張子 .jpg"));
    }
}
