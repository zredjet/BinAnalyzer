using System.Text.RegularExpressions;

namespace BinAnalyzer.Presentation;

public enum YamlTokenKind { Plain, Key, String, Number, Comment, Punct }

public sealed record YamlToken(string Text, YamlTokenKind Kind);

/// <summary>行単位の簡易 YAML トークナイザ（表示用。構文解析はしない）。</summary>
public static partial class YamlHighlighter
{
    [GeneratedRegex(@"(?<comment>#.*$)|(?<string>""(?:[^""\\]|\\.)*""|'[^']*')|(?<key>[A-Za-z0-9_\-]+(?=\s*:(\s|$)))|(?<number>\b(?:0x[0-9A-Fa-f]+|\d+(?:\.\d+)?)\b)|(?<punct>[:\-\[\]\{\},])|(?<plain>\s+|[^\s:\-\[\]\{\},#""']+|.)")]
    private static partial Regex Token();

    public static IReadOnlyList<YamlToken> Tokenize(string line)
    {
        var tokens = new List<YamlToken>();
        foreach (Match m in Token().Matches(line))
        {
            var kind = m.Groups["comment"].Success ? YamlTokenKind.Comment
                : m.Groups["string"].Success ? YamlTokenKind.String
                : m.Groups["key"].Success ? YamlTokenKind.Key
                : m.Groups["number"].Success ? YamlTokenKind.Number
                : m.Groups["punct"].Success ? YamlTokenKind.Punct
                : YamlTokenKind.Plain;
            tokens.Add(new YamlToken(m.Value, kind));
        }
        return tokens;
    }
}
