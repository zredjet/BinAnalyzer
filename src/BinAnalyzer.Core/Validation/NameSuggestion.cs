namespace BinAnalyzer.Core.Validation;

/// <summary>
/// 書き間違いの候補（「もしかして」）。DSL のキー（VAL123）・式の名前（VAL124）・関数名（VAL125）で共有する。
/// </summary>
public static class NameSuggestion
{
    /// <summary>候補とみなす編集距離の上限。</summary>
    public const int MaxDistance = 2;

    /// <summary>
    /// <paramref name="candidates"/> のうち、<paramref name="name"/> との編集距離（Levenshtein、大文字小文字を無視）が最小で
    /// <see cref="MaxDistance"/> 以内のもの。同点は列挙順で先のもの。無ければ null。
    /// </summary>
    public static string? Suggest(string name, IEnumerable<string> candidates)
    {
        var lower = name.ToLowerInvariant();
        string? best = null;
        var bestDistance = MaxDistance + 1;
        foreach (var candidate in candidates)
        {
            var d = Levenshtein(lower, candidate.ToLowerInvariant());
            if (d < bestDistance)
            {
                best = candidate;
                bestDistance = d;
            }
        }
        return best;
    }

    private static int Levenshtein(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[b.Length];
    }
}
