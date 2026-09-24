using System.Reflection;
using System.Text.RegularExpressions;
using BinAnalyzer.Core.Validation;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace BinAnalyzer.Integration.Tests;

/// <summary>
/// ドキュメントと実装の整合性チェック（REQ-183）。
/// 各チェックは「文書テキスト + 実装側の集合 → 不一致メッセージ」の純関数で、空リストなら一致。
/// 文書側の照合範囲は <c>&lt;!-- doc-sync: 名前 --&gt;</c> 〜 <c>&lt;!-- /doc-sync --&gt;</c> で示す。
/// </summary>
internal static class DocConsistency
{
    private const string RegionEnd = "<!-- /doc-sync -->";

    /// <summary>README の表示名とファイル名で正規化しても一致しないもの（表示名 → ファイル名）。</summary>
    private static readonly Dictionary<string, string> FormatNameAliases = new()
    {
        ["messagepack"] = "msgpack",
    };

    /// <summary>マーカー <paramref name="name"/> で囲まれた範囲。無ければ null とメッセージ。</summary>
    public static string? Region(string doc, string name, List<string> problems)
    {
        var begin = $"<!-- doc-sync: {name} -->";
        var start = doc.IndexOf(begin, StringComparison.Ordinal);
        if (start < 0)
        {
            problems.Add($"マーカー `{name}` が見つかりません");
            return null;
        }
        start += begin.Length;
        var end = doc.IndexOf(RegionEnd, start, StringComparison.Ordinal);
        if (end < 0)
        {
            problems.Add($"マーカー `{name}` が閉じられていません（{RegionEnd} がない）");
            return null;
        }
        return doc[start..end];
    }

    /// <summary>README のフォーマット一覧（<c>（A, B — 計N種）</c>）と formats/*.bdef.yaml の比較。</summary>
    public static List<string> CheckFormats(string readme, IReadOnlyCollection<string> formatFileStems)
    {
        var problems = new List<string>();
        var region = Region(readme, "formats", problems);
        if (region is null)
            return problems;

        var m = Regex.Match(region, @"（(?<names>[^（）]+?)\s*—\s*計(?<n>\d+)種）");
        if (!m.Success)
        {
            problems.Add("README の formats に「（A, B, … — 計N種）」の形の一覧がありません");
            return problems;
        }

        var count = int.Parse(m.Groups["n"].Value);
        var names = m.Groups["names"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (count != formatFileStems.Count)
            problems.Add($"README の件数「計{count}種」が formats/ の定義数 {formatFileStems.Count} と一致しません");
        if (names.Length != count)
            problems.Add($"README の列挙 {names.Length} 件が「計{count}種」と一致しません");

        var documented = names.Select(DocumentedFormatKey).ToList();
        var files = formatFileStems.ToDictionary(NormalizeFormatName, s => s);
        foreach (var dup in documented.GroupBy(n => n).Where(g => g.Count() > 1))
            problems.Add($"README の列挙に重複があります: {dup.Key}");
        foreach (var name in names.Where((_, i) => !files.ContainsKey(documented[i])))
            problems.Add($"README の「{name}」に対応する formats/*.bdef.yaml がありません");
        foreach (var (key, stem) in files.Where(f => !documented.Contains(f.Key)))
            problems.Add($"formats/{stem}.bdef.yaml が README の列挙にありません");
        return problems;
    }

    private static string DocumentedFormatKey(string displayName)
    {
        var key = NormalizeFormatName(displayName);
        return FormatNameAliases.GetValueOrDefault(key, key);
    }

    private static string NormalizeFormatName(string name) =>
        Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]", "");

    /// <summary>dsl-reference.md の対応アルゴリズム表（名前・カテゴリ）と実装の比較。</summary>
    public static List<string> CheckChecksumAlgorithms(
        string dslReference, IReadOnlySet<string> integerAlgorithms, IReadOnlySet<string> hashAlgorithms)
    {
        var problems = new List<string>();
        var region = Region(dslReference, "checksum-algorithms", problems);
        if (region is null)
            return problems;

        var rows = Regex.Matches(region, @"^\|\s*`(?<name>[^`]+)`\s*\|\s*(?<cat>[^|]+?)\s*\|", RegexOptions.Multiline);
        var documented = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match row in rows)
        {
            if (!documented.TryAdd(row.Groups["name"].Value, row.Groups["cat"].Value))
                problems.Add($"対応アルゴリズム表に重複があります: {row.Groups["name"].Value}");
        }

        CompareCategory("整数系", integerAlgorithms);
        CompareCategory("ハッシュ系", hashAlgorithms);
        foreach (var (name, cat) in documented.Where(d => d.Value is not ("整数系" or "ハッシュ系")))
            problems.Add($"対応アルゴリズム表の `{name}` のカテゴリ「{cat}」は不明です（整数系 / ハッシュ系）");
        return problems;

        void CompareCategory(string category, IReadOnlySet<string> implemented)
        {
            var doc = documented.Where(d => d.Value == category).Select(d => d.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var name in implemented.Where(a => !doc.Contains(a)).Order())
                problems.Add($"{category}アルゴリズム `{name}` が対応アルゴリズム表にありません（またはカテゴリ違い）");
            foreach (var name in doc.Where(a => !implemented.Contains(a)).Order())
                problems.Add($"対応アルゴリズム表の {category} `{name}` は実装にありません");
        }
    }

    /// <summary>dsl-reference.md の圧縮型一覧（最初の <c>（…）</c> 内のバッククォート）と実装の比較。</summary>
    public static List<string> CheckCompressionTypes(string dslReference, IReadOnlyCollection<string> implementedTypeNames)
    {
        var problems = new List<string>();
        var region = Region(dslReference, "compression-types", problems);
        if (region is null)
            return problems;

        var paren = Regex.Match(region, "（(?<list>[^）]*)）");
        var documented = Regex.Matches(paren.Groups["list"].Value, "`([^`]+)`").Select(m => m.Groups[1].Value).ToHashSet();
        foreach (var name in implementedTypeNames.Where(n => !documented.Contains(n)).Order())
            problems.Add($"圧縮型 `{name}` が dsl-reference.md の圧縮型一覧にありません");
        foreach (var name in documented.Where(n => !implementedTypeNames.Contains(n)).Order())
            problems.Add($"dsl-reference.md の圧縮型 `{name}` は実装にありません");
        return problems;
    }

    /// <summary>dsl-reference.md の「組み込み関数」節の見出し（<c>#### `name(…)`</c>）と、Core の組み込み関数の一覧の比較（REQ-189）。</summary>
    public static List<string> CheckBuiltinFunctions(string dslReference, IReadOnlyCollection<string> implementedNames)
    {
        var problems = new List<string>();
        var section = Regex.Match(dslReference, @"^### 組み込み関数\n(?<body>.*?)(?=^#{2,3} |\z)", RegexOptions.Multiline | RegexOptions.Singleline);
        if (!section.Success)
        {
            problems.Add("dsl-reference.md に「### 組み込み関数」の節がありません");
            return problems;
        }
        var documented = Regex.Matches(section.Groups["body"].Value, @"^#### `(?<name>\w+)\(", RegexOptions.Multiline)
            .Select(m => m.Groups["name"].Value).ToHashSet();
        foreach (var name in implementedNames.Where(n => !documented.Contains(n)).Order())
            problems.Add($"組み込み関数 `{name}` が dsl-reference.md の組み込み関数の節にありません");
        foreach (var name in documented.Where(n => !implementedNames.Contains(n)).Order())
            problems.Add($"dsl-reference.md の組み込み関数 `{name}` は BuiltinFunctions.Names にありません");
        return problems;
    }

    /// <summary>
    /// FormatValidator のソースから検証コードと重大度を集める。コードは <c>Error("VALnnn", …)</c> / <c>Warning("VALnnn", …)</c>
    /// のリテラルで渡している前提で、その書き方から外れた呼び出し（抜き出し漏れ）は problems に入れる。
    /// </summary>
    public static Dictionary<string, DiagnosticSeverity> ExtractValidatorCodes(string validatorSource, List<string> problems)
    {
        var codes = new Dictionary<string, DiagnosticSeverity>();
        var literalCalls = Regex.Matches(validatorSource, @"\b(?<sev>Error|Warning)\(""(?<code>VAL\d{3})""");
        foreach (Match m in literalCalls)
        {
            var severity = m.Groups["sev"].Value == "Error" ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning;
            var code = m.Groups["code"].Value;
            if (codes.TryGetValue(code, out var existing) && existing != severity)
                problems.Add($"{code} がエラーと警告の両方で使われています");
            codes[code] = severity;
        }

        // 定義（private static ValidationDiagnostic Error(string code, …) / Warning(…)）の 2 箇所を除く全出現がリテラル呼び出しであること
        var allCalls = Regex.Matches(validatorSource, @"\b(Error|Warning)\(").Count;
        if (allCalls != literalCalls.Count + 2)
            problems.Add($"FormatValidator の Error( / Warning( の出現 {allCalls} 件のうち、コードのリテラル付き呼び出しは {literalCalls.Count} 件です（定義 2 件を除いて一致する必要があります）");
        if (codes.Count == 0)
            problems.Add("FormatValidator から検証コードを 1 件も抜き出せませんでした");
        return codes;
    }

    /// <summary>parser-design.md の検証コード一覧（<c>- VALnnn:</c>、直前の <c>**エラー</c> / <c>**警告</c> で重大度）と実装の比較。</summary>
    public static List<string> CheckValidationCodes(string parserDesign, IReadOnlyDictionary<string, DiagnosticSeverity> implemented)
    {
        var problems = new List<string>();
        var region = Region(parserDesign, "validation-codes", problems);
        if (region is null)
            return problems;

        var documented = new Dictionary<string, DiagnosticSeverity>();
        DiagnosticSeverity? current = null;
        foreach (var line in region.Split('\n'))
        {
            if (line.StartsWith("**エラー", StringComparison.Ordinal))
                current = DiagnosticSeverity.Error;
            else if (line.StartsWith("**警告", StringComparison.Ordinal))
                current = DiagnosticSeverity.Warning;
            else if (Regex.Match(line, @"^- (VAL\d{3}):") is { Success: true } m)
            {
                var code = m.Groups[1].Value;
                if (current is null)
                    problems.Add($"parser-design.md の {code} がエラー / 警告の見出しの前にあります");
                else if (!documented.TryAdd(code, current.Value))
                    problems.Add($"parser-design.md の検証コード一覧に {code} が重複しています");
            }
        }

        foreach (var (code, severity) in implemented.OrderBy(c => c.Key))
        {
            if (!documented.TryGetValue(code, out var doc))
                problems.Add($"{code}（{SeverityName(severity)}）が parser-design.md の検証コード一覧にありません");
            else if (doc != severity)
                problems.Add($"{code} は実装では{SeverityName(severity)}ですが、parser-design.md では{SeverityName(doc)}に書かれています");
        }
        foreach (var code in documented.Keys.Where(c => !implemented.ContainsKey(c)).Order())
            problems.Add($"parser-design.md の {code} は実装にありません");
        return problems;
    }

    private static string SeverityName(DiagnosticSeverity s) => s == DiagnosticSeverity.Error ? "エラー" : "警告";

    /// <summary>文書中の <c>VALnnn</c> がすべて実装に存在し、範囲表記（<c>VALnnn〜VALnnn</c>）が無いこと。</summary>
    public static List<string> CheckValidationCodeMentions(
        IEnumerable<(string File, string Text)> docs, IReadOnlyCollection<string> implementedCodes)
    {
        var problems = new List<string>();
        foreach (var (file, text) in docs)
        {
            foreach (Match m in Regex.Matches(text, @"VAL\d{3}\s*[〜～~\-–]\s*VAL\d{3}"))
                problems.Add($"{file}:{LineOf(text, m.Index)}: 検証コードの範囲表記「{m.Value}」は欠番や 1xx 番台のエラーを表せません。parser-design.md の一覧を参照してください");
            foreach (Match m in Regex.Matches(text, @"\bVAL\d{3}\b"))
            {
                if (!implementedCodes.Contains(m.Value))
                    problems.Add($"{file}:{LineOf(text, m.Index)}: {m.Value} は実装にありません");
            }
        }
        return problems;
    }

    /// <summary>architecture.md のプロジェクト構成と slnx のプロジェクト集合の比較。</summary>
    public static List<string> CheckProjectTree(string architecture, IReadOnlyCollection<string> solutionProjects)
    {
        var problems = new List<string>();
        var region = Region(architecture, "project-tree", problems);
        if (region is null)
            return problems;

        var documented = Regex.Matches(region, @"BinAnalyzer(?:\.[A-Za-z]+)+").Select(m => m.Value).ToHashSet();
        foreach (var p in solutionProjects.Where(p => !documented.Contains(p)).Order())
            problems.Add($"{p} が architecture.md のプロジェクト構成にありません");
        foreach (var p in documented.Where(p => !solutionProjects.Contains(p)).Order())
            problems.Add($"architecture.md のプロジェクト構成の {p} はソリューションにありません");
        return problems;
    }

    /// <summary>
    /// architecture.md の依存関係図（<c>A → B, C（+ パッケージ）</c>）と src の ProjectReference（直接参照）の比較。
    /// プロジェクト名は <c>BinAnalyzer.</c> を除いた短縮名。
    /// </summary>
    public static List<string> CheckProjectDependencies(
        string architecture, IReadOnlyDictionary<string, IReadOnlySet<string>> projectReferences)
    {
        var problems = new List<string>();
        var region = Region(architecture, "project-dependencies", problems);
        if (region is null)
            return problems;

        var documented = new Dictionary<string, HashSet<string>>();
        foreach (Match m in Regex.Matches(region, @"^(?<from>\S+) → (?<to>.*)$", RegexOptions.Multiline))
        {
            var targets = Regex.Replace(m.Groups["to"].Value, "（[^）]*）", "")
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (!documented.TryAdd(m.Groups["from"].Value, targets.ToHashSet()))
                problems.Add($"依存関係図に {m.Groups["from"].Value} の行が重複しています");
        }

        foreach (var (project, refs) in projectReferences.OrderBy(p => p.Key))
        {
            if (!documented.TryGetValue(project, out var doc))
            {
                problems.Add($"依存関係図に {project} の行がありません");
                continue;
            }
            foreach (var r in refs.Where(r => !doc.Contains(r)).Order())
                problems.Add($"依存関係図の {project} に {r} がありません（{project}.csproj は参照している）");
            foreach (var r in doc.Where(r => !refs.Contains(r)).Order())
                problems.Add($"依存関係図の {project} → {r} は {project}.csproj の ProjectReference にありません");
        }
        foreach (var project in documented.Keys.Where(p => !projectReferences.ContainsKey(p)).Order())
            problems.Add($"依存関係図の {project} は src のプロジェクトにありません");
        return problems;
    }

    /// <summary>YAML DTO が受け付けるキー（<c>[YamlMember(Alias)]</c>）。</summary>
    public static IReadOnlySet<string> YamlKeysOf<T>() =>
        typeof(T).GetProperties()
            .Select(p => p.GetCustomAttribute<YamlMemberAttribute>()?.Alias)
            .OfType<string>()
            .ToHashSet();

    /// <summary>
    /// 文書の ```yaml ブロックに、DSL が受け付けないキーが無いこと。
    /// <c>name</c> とスカラーの <c>type</c> を持つマッピングをフィールド定義、その <c>checksum:</c> をチェックサム定義、
    /// <c>structs:</c> の各値（マッピング）を struct 定義とみなす。
    /// </summary>
    public static List<string> CheckYamlExampleKeys(
        IEnumerable<(string File, string Text)> docs,
        IReadOnlySet<string> fieldKeys, IReadOnlySet<string> structKeys, IReadOnlySet<string> checksumKeys)
    {
        var problems = new List<string>();
        foreach (var (file, text) in docs)
        {
            foreach (Match block in Regex.Matches(text, @"^```ya?ml[ \t]*\n(?<body>.*?)^```", RegexOptions.Multiline | RegexOptions.Singleline))
            {
                var body = block.Groups["body"];
                var baseLine = LineOf(text, body.Index) - 1;
                var stream = new YamlStream();
                try
                {
                    stream.Load(new StringReader(body.Value));
                }
                catch (YamlException e)
                {
                    problems.Add($"{file}:{baseLine + (int)e.Start.Line}: YAML として読めません（意図的に不正な例は ```text 等にしてください）: {e.Message}");
                    continue;
                }
                foreach (var document in stream.Documents)
                    Walk(document.RootNode);

                void Walk(YamlNode node)
                {
                    switch (node)
                    {
                        case YamlMappingNode map:
                            if (IsFieldDefinition(map))
                            {
                                CheckKeys(map, fieldKeys, $"フィールド '{Scalar(map, "name")}'");
                                if (Child(map, "checksum") is YamlMappingNode checksum)
                                    CheckKeys(checksum, checksumKeys, $"フィールド '{Scalar(map, "name")}' の checksum");
                            }
                            if (Child(map, "structs") is YamlMappingNode structs)
                            {
                                foreach (var (key, value) in structs.Children)
                                {
                                    if (value is YamlMappingNode structMap)
                                        CheckKeys(structMap, structKeys, $"struct '{key}'");
                                }
                            }
                            foreach (var child in map.Children.Values)
                                Walk(child);
                            break;
                        case YamlSequenceNode seq:
                            foreach (var child in seq.Children)
                                Walk(child);
                            break;
                    }
                }

                void CheckKeys(YamlMappingNode map, IReadOnlySet<string> allowed, string what)
                {
                    foreach (var key in map.Children.Keys.OfType<YamlScalarNode>())
                    {
                        if (!allowed.Contains(key.Value!))
                            problems.Add($"{file}:{baseLine + (int)key.Start.Line}: {what} のキー `{key.Value}` は DSL にありません（未知キーは無視され、エラーにもなりません）");
                    }
                }
            }
        }
        return problems;
    }

    private static bool IsFieldDefinition(YamlMappingNode map) =>
        Child(map, "name") is YamlScalarNode && Child(map, "type") is YamlScalarNode;

    private static YamlNode? Child(YamlMappingNode map, string key) =>
        map.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value : null;

    private static string? Scalar(YamlMappingNode map, string key) => (Child(map, key) as YamlScalarNode)?.Value;

    /// <summary>範囲内の最初の ```yaml ブロックの本文。</summary>
    public static string? FirstYamlBlock(string region) =>
        Regex.Match(region, @"^```ya?ml[ \t]*\n(?<body>.*?)^```", RegexOptions.Multiline | RegexOptions.Singleline) is { Success: true } m
            ? m.Groups["body"].Value
            : null;

    private static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;
}
