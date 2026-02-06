namespace BinAnalyzer.Core;

/// <summary>
/// パスパターンマッチャー。ドット区切りのフィールドパスに対してワイルドカードマッチを行う。
/// </summary>
public sealed class PathFilter
{
    private readonly string[][] _patterns;

    public PathFilter(IEnumerable<string> patterns)
    {
        _patterns = patterns.Select(p => p.Split('.')).ToArray();
    }

    /// <summary>パスがいずれかのパターンに完全マッチするか。</summary>
    public bool Matches(string path)
    {
        var segments = path.Split('.');
        return _patterns.Any(pattern => MatchesPattern(segments, 0, pattern, 0));
    }

    /// <summary>パスがいずれかのパターンのマッチの祖先であるか。</summary>
    public bool IsAncestorOfMatch(string path)
    {
        var segments = path.Split('.');
        return _patterns.Any(pattern => IsAncestorOfPattern(segments, pattern));
    }

    /// <summary>パスがマッチまたはマッチの祖先であるか。ツリー出力で祖先を保持するために使用。</summary>
    public bool ShouldInclude(string path)
    {
        return Matches(path) || IsAncestorOfMatch(path);
    }

    private static bool MatchesPattern(string[] segments, int si, string[] pattern, int pi)
    {
        while (si < segments.Length && pi < pattern.Length)
        {
            if (pattern[pi] == "**")
            {
                // ** は0個以上の任意のセグメントにマッチ
                // 残りのパターンがない場合は全マッチ
                if (pi == pattern.Length - 1)
                    return true;

                // 0個以上のセグメントを消費して残りをマッチ
                for (var skip = 0; skip <= segments.Length - si; skip++)
                {
                    if (MatchesPattern(segments, si + skip, pattern, pi + 1))
                        return true;
                }
                return false;
            }

            if (pattern[pi] == "*")
            {
                // * は任意の1セグメントにマッチ
                si++;
                pi++;
                continue;
            }

            if (segments[si] != pattern[pi])
                return false;

            si++;
            pi++;
        }

        // 残りのパターンが全て ** のみの場合もマッチ
        while (pi < pattern.Length && pattern[pi] == "**")
            pi++;

        return si == segments.Length && pi == pattern.Length;
    }

    private static bool IsAncestorOfPattern(string[] segments, string[] pattern)
    {
        // segmentsがpatternより短い場合のみ祖先の可能性がある
        // segmentsの各要素がpatternの対応する要素にマッチするか確認
        return IsAncestorMatch(segments, 0, pattern, 0);
    }

    private static bool IsAncestorMatch(string[] segments, int si, string[] pattern, int pi)
    {
        // segmentsを全て消費した場合、patternにまだ残りがあれば祖先
        if (si == segments.Length)
            return pi < pattern.Length;

        if (pi >= pattern.Length)
            return false;

        if (pattern[pi] == "**")
        {
            // ** は0個以上のセグメントを消費
            // segmentsが残っている場合、まだ祖先の可能性がある
            if (pi == pattern.Length - 1)
            {
                // パターンの最後が ** なら、segmentsは常にマッチ（祖先ではない）
                return false;
            }

            for (var skip = 0; skip <= segments.Length - si; skip++)
            {
                if (IsAncestorMatch(segments, si + skip, pattern, pi + 1))
                    return true;
            }
            return false;
        }

        if (pattern[pi] == "*" || segments[si] == pattern[pi])
        {
            return IsAncestorMatch(segments, si + 1, pattern, pi + 1);
        }

        return false;
    }
}
