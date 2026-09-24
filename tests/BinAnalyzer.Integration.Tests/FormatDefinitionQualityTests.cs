using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace BinAnalyzer.Integration.Tests;

/// <summary>
/// 同梱のフォーマット定義が書き方の規約（docs/format-authoring.md、REQ-188）を満たすこと。
/// 先頭コメントの「# 仕様:」「# 対応していないもの:」と、全フィールドの日本語の description を検査する。
/// 見直しが済んでいない定義は <see cref="Pending"/> に載せ、見直した定義から外していく。
/// </summary>
public sealed class FormatDefinitionQualityTests
{
    private static readonly string FormatsDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats"));

    /// <summary>
    /// 見直し待ちの定義（formats/ からの相対パス）。規約を満たした定義は一覧から外すこと（外し忘れるとテストが知らせる）。
    /// 一覧に無い定義（新しく追加する定義を含む）は規約を満たさないと失敗する。
    /// </summary>
    private static readonly HashSet<string> Pending = new(StringComparer.Ordinal)
    {
        // 音声・映像
        "mp3.bdef.yaml",
        "mp4.bdef.yaml",
        "wav.bdef.yaml",
        "flac.bdef.yaml",
        "ogg.bdef.yaml",
        "avi.bdef.yaml",
        "flv.bdef.yaml",
        "midi.bdef.yaml",
        "mkv.bdef.yaml",
        "common/riff.bdef.yaml",
        "common/isobmff.bdef.yaml",
        // データ・その他
        "sqlite.bdef.yaml",
        "parquet.bdef.yaml",
        "pdf.bdef.yaml",
        "pcap.bdef.yaml",
        "dns.bdef.yaml",
        "protobuf.bdef.yaml",
        "msgpack.bdef.yaml",
        "cbor.bdef.yaml",
        "x509.bdef.yaml",
        "fat.bdef.yaml",
        "otf.bdef.yaml",
    };

    private static readonly Regex Japanese = new(@"[぀-ヿ㐀-鿿]", RegexOptions.Compiled);

    private static List<string> AllDefinitions() =>
        Directory.GetFiles(FormatsDir, "*.bdef.yaml", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(FormatsDir, path).Replace('\\', '/'))
            .Order()
            .ToList();

    public static TheoryData<string> Definitions() => new(AllDefinitions());

    /// <summary>規約に反する箇所の一覧（空なら満たしている）。</summary>
    internal static List<string> Check(string yaml)
    {
        var problems = new List<string>();

        // 先頭コメント: 最初の YAML の行より前のコメント
        var header = yaml.Replace("\r\n", "\n").Split('\n')
            .TakeWhile(l => l.Length == 0 || l.StartsWith('#'))
            .ToList();
        if (!header.Any(l => l.StartsWith("# 仕様:", StringComparison.Ordinal)))
            problems.Add("先頭コメントに「# 仕様:」の行がありません");
        if (!header.Any(l => l.StartsWith("# 対応していないもの:", StringComparison.Ordinal)))
            problems.Add("先頭コメントに「# 対応していないもの:」の行がありません");

        // 全フィールドの description
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        var root = (YamlMappingNode)stream.Documents[0].RootNode;
        if (!root.Children.TryGetValue(new YamlScalarNode("structs"), out var structsNode) || structsNode is not YamlMappingNode structs)
            return problems;

        var missing = new List<string>();
        var notJapanese = new List<string>();
        foreach (var (nameNode, body) in structs.Children)
        {
            var fields = body switch
            {
                YamlSequenceNode seq => seq,
                YamlMappingNode map when map.Children.TryGetValue(new YamlScalarNode("fields"), out var f) => f as YamlSequenceNode,
                _ => null,
            };
            foreach (var field in fields?.Children.OfType<YamlMappingNode>() ?? [])
            {
                var name = $"{((YamlScalarNode)nameNode).Value}.{Scalar(field, "name")}";
                var description = Scalar(field, "description");
                if (string.IsNullOrWhiteSpace(description))
                    missing.Add(name);
                else if (!Japanese.IsMatch(description))
                    notJapanese.Add(name);
            }
        }
        if (missing.Count > 0)
            problems.Add($"description の無いフィールドが {missing.Count} 件: {Sample(missing)}");
        if (notJapanese.Count > 0)
            problems.Add($"description が日本語でないフィールドが {notJapanese.Count} 件: {Sample(notJapanese)}");
        return problems;
    }

    private static string? Scalar(YamlMappingNode map, string key) =>
        map.Children.TryGetValue(new YamlScalarNode(key), out var v) ? (v as YamlScalarNode)?.Value : null;

    private static string Sample(List<string> names) =>
        string.Join(", ", names.Take(5)) + (names.Count > 5 ? " ほか" : "");

    [Theory]
    [MemberData(nameof(Definitions))]
    public void Definition_FollowsTheAuthoringGuide(string relativePath)
    {
        var problems = Check(File.ReadAllText(Path.Combine(FormatsDir, relativePath)));

        if (Pending.Contains(relativePath))
        {
            problems.Should().NotBeEmpty(
                $"{relativePath} は規約を満たしています。FormatDefinitionQualityTests.Pending から外してください");
            return;
        }
        problems.Should().BeEmpty($"{relativePath} が docs/format-authoring.md の規約を満たしていません");
    }

    [Fact]
    public void Pending_ListsOnlyExistingDefinitions()
    {
        var existing = AllDefinitions().ToHashSet();
        Pending.Where(p => !existing.Contains(p)).Should().BeEmpty("対応待ち一覧に存在しない定義があります");
    }

    // --- 検査そのもののテスト ---

    private const string Good = """
        # テスト形式
        # 仕様: テスト仕様 1.0
        # 対応していないもの: なし
        name: T
        root: t
        structs:
          t:
            - name: magic
              type: uint8
              description: "マジック"
          legacy:
            fields:
              - name: size
                type: uint8
                description: "サイズ（バイト）"
        """;

    [Fact]
    public void Check_AcceptsAConformingDefinition()
    {
        Check(Good).Should().BeEmpty();
    }

    [Fact]
    public void Check_ReportsMissingHeaderLines()
    {
        var problems = Check(Good.Replace("# 仕様: テスト仕様 1.0\n", "").Replace("# 対応していないもの: なし\n", ""));

        problems.Should().Contain(p => p.Contains("# 仕様:")).And.Contain(p => p.Contains("# 対応していないもの:"));
    }

    [Fact]
    public void Check_ReportsMissingAndEnglishDescriptions()
    {
        var yaml = Good.Replace("      description: \"マジック\"\n", "")
            .Replace("description: \"サイズ（バイト）\"", "description: \"size in bytes\"");

        var problems = Check(yaml);

        problems.Should().Contain(p => p.Contains("description の無いフィールドが 1 件: t.magic"));
        problems.Should().Contain(p => p.Contains("日本語でないフィールドが 1 件: legacy.size"));
    }
}
