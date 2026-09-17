namespace BinAnalyzer.Dsl;

/// <summary>
/// 相対インポートパスの字句的な結合。ファイルシステムに依存せず、URL の相対パスとしてもそのまま使える。
/// </summary>
public static class ImportPath
{
    /// <summary>
    /// <paramref name="basePath"/>（ファイルの識別子）が置かれたディレクトリから見た <paramref name="importPath"/> を結合し、
    /// <c>.</c> / <c>..</c> / 空セグメントを取り除いた <c>/</c> 区切りのパスを返す。
    /// <paramref name="importPath"/> が <c>/</c> で始まる場合は基準を無視してそのまま正規化する。
    /// </summary>
    public static string Combine(string basePath, string importPath)
    {
        var import = importPath.Replace('\\', '/');
        if (import.StartsWith('/'))
            return "/" + Normalize(import);

        var b = basePath.Replace('\\', '/');
        var slash = b.LastIndexOf('/');
        var baseDir = slash < 0 ? "" : b[..(slash + 1)];
        var rooted = b.StartsWith('/');
        var combined = Normalize(baseDir + import);
        return rooted ? "/" + combined : combined;
    }

    private static string Normalize(string path)
    {
        var result = new List<string>();
        foreach (var seg in path.Split('/'))
        {
            if (seg.Length == 0 || seg == ".") continue;
            if (seg == ".." && result.Count > 0 && result[^1] != "..")
            {
                result.RemoveAt(result.Count - 1);
                continue;
            }
            result.Add(seg);
        }
        return string.Join('/', result);
    }
}
