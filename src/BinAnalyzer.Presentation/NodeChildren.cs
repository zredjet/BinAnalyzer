using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Presentation;

/// <summary>
/// デコード結果ツリーの子ノード走査。表示系（TUI/GUI）で「展開したときに何が並ぶか」の単一情報源。
/// struct は padding を除外、array は要素、compressed は展開後の内容の子を返す。
/// </summary>
public static class NodeChildren
{
    public static IEnumerable<DecodedNode> Of(DecodedNode node)
    {
        return node switch
        {
            DecodedStruct s => s.Children.Where(c => !c.IsPadding),
            DecodedArray a => a.Elements,
            DecodedCompressed c when c.DecodedContent is not null => c.DecodedContent.Children.Where(ch => !ch.IsPadding),
            _ => [],
        };
    }

    /// <summary><see cref="Of"/> が返す子の数。配列は要素数、struct は padding を除いた数。</summary>
    public static int Count(DecodedNode node)
    {
        return node switch
        {
            DecodedStruct s => CountNonPadding(s.Children),
            DecodedArray a => a.Elements.Count,
            DecodedCompressed c when c.DecodedContent is not null => CountNonPadding(c.DecodedContent.Children),
            _ => 0,
        };
    }

    private static int CountNonPadding(IReadOnlyList<DecodedNode> children)
    {
        var n = 0;
        for (var i = 0; i < children.Count; i++)
            if (!children[i].IsPadding) n++;
        return n;
    }

    public static bool HasChildren(DecodedNode node)
    {
        return node switch
        {
            DecodedStruct s => s.Children.Any(c => !c.IsPadding),
            DecodedArray a => a.Elements.Count > 0,
            DecodedCompressed c => c.DecodedContent?.Children.Any(ch => !ch.IsPadding) == true,
            _ => false,
        };
    }

    /// <summary>ルートから深さ優先（親→子の順）で全ノードを列挙する。padding は含まない。</summary>
    public static IEnumerable<DecodedNode> Descendants(DecodedNode root, bool includeSelf = true)
    {
        if (includeSelf)
            yield return root;
        foreach (var child in Of(root))
        {
            foreach (var d in Descendants(child, includeSelf: true))
                yield return d;
        }
    }
}
