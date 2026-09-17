using System.Text.RegularExpressions;

namespace BinAnalyzer.Presentation;

/// <summary>0 始まりの行番号。</summary>
public sealed record YamlLocation(int StructLine, int? FieldLine);

/// <summary>
/// フォーマット定義 YAML のテキストから、struct ブロックとその中のフィールド定義行を探す。
/// DSL パーサーは行番号を保持しないため、テキスト探索で近似する（インポートされた struct は見つからない → null）。
/// </summary>
public static partial class YamlFieldLocator
{
    [GeneratedRegex(@"^(\s*)([A-Za-z0-9_\-]+):\s*(#.*)?$")]
    private static partial Regex KeyLine();

    [GeneratedRegex(@"^\s*-\s*name:\s*[""']?([^""'#\s]+)[""']?\s*(#.*)?$")]
    private static partial Regex NameLine();

    public static YamlLocation? Locate(string yaml, string structName, string? fieldName)
    {
        var lines = SplitLines(yaml);
        var block = StructBlock(lines, structName);
        if (block is null)
            return null;
        var (start, end) = block.Value;
        if (fieldName is null)
            return new YamlLocation(start, null);
        for (var i = start + 1; i < end; i++)
        {
            var m = NameLine().Match(lines[i]);
            if (m.Success && m.Groups[1].Value == fieldName)
                return new YamlLocation(start, i);
        }
        return new YamlLocation(start, null);
    }

    public static int? LocateStruct(string yaml, string structName)
        => StructBlock(SplitLines(yaml), structName)?.Start;

    /// <summary><c>structs:</c> 配下で <paramref name="structName"/> のブロック [start, end) を返す。</summary>
    private static (int Start, int End)? StructBlock(string[] lines, string structName)
    {
        var structsLine = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            var m = KeyLine().Match(lines[i]);
            if (m.Success && m.Groups[1].Length == 0 && m.Groups[2].Value == "structs")
            {
                structsLine = i;
                break;
            }
        }
        if (structsLine < 0)
            return null;

        var childIndent = -1;
        var start = -1;
        for (var i = structsLine + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (IsBlankOrComment(line))
                continue;
            var indent = Indent(line);
            if (indent == 0)
                break; // structs: セクション終了
            if (childIndent < 0)
                childIndent = indent;
            if (indent != childIndent)
                continue;
            var m = KeyLine().Match(line);
            if (start >= 0)
                return (start, i);
            if (m.Success && m.Groups[2].Value == structName)
                start = i;
        }
        if (start < 0)
            return null;
        // ブロックの終わり: structs セクション内で次の同インデント行、なければ次のインデント 0 行（末尾）
        var endLine = lines.Length;
        for (var i = start + 1; i < lines.Length; i++)
        {
            if (IsBlankOrComment(lines[i]))
                continue;
            if (Indent(lines[i]) <= childIndent)
            {
                endLine = i;
                break;
            }
        }
        return (start, endLine);
    }

    private static string[] SplitLines(string yaml) => yaml.Replace("\r\n", "\n").Split('\n');
    private static bool IsBlankOrComment(string line) => line.Trim().Length == 0 || line.TrimStart().StartsWith('#');
    private static int Indent(string line) => line.Length - line.TrimStart(' ').Length;
}
