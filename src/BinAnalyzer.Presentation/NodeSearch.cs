using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Presentation;

/// <summary>ノード名によるツリー検索。</summary>
public static class NodeSearch
{
    /// <summary>名前に <paramref name="query"/> を含むノードを深さ優先順で返す（大文字小文字を区別しない）。</summary>
    public static List<DecodedNode> ByName(DecodedNode root, string query)
    {
        var results = new List<DecodedNode>();
        if (string.IsNullOrEmpty(query))
            return results;
        Collect(root, query, results);
        return results;
    }

    private static void Collect(DecodedNode node, string query, List<DecodedNode> results)
    {
        if (node.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            results.Add(node);

        switch (node)
        {
            case DecodedStruct s:
                foreach (var child in s.Children)
                    Collect(child, query, results);
                break;
            case DecodedArray a:
                foreach (var element in a.Elements)
                    Collect(element, query, results);
                break;
            case DecodedCompressed c when c.DecodedContent is not null:
                foreach (var child in c.DecodedContent.Children)
                    Collect(child, query, results);
                break;
        }
    }
}
