namespace BinAnalyzer.Presentation;

/// <summary>
/// ツリー表示の可視 1 行。<see cref="MoreRemaining"/> が正なら「さらに表示（残り N）」の行で、
/// <see cref="Id"/> はその親（配列など）のノード ID。
/// </summary>
public readonly record struct TreeRow(int Id, int Depth, bool HasChildren, bool Expanded, int MoreRemaining)
{
    public bool IsMore => MoreRemaining > 0;
}

/// <summary>
/// 展開状態から「いま画面に並ぶ行」を平坦なリストにする。ネストした描画をやめて仮想化できるようにするためのもの（REQ-177）。
/// 子が <paramref name="limitOf"/> を超えるノードは先頭からその数だけ並べ、末尾に「さらに表示」行を置く。
/// </summary>
public static class TreeRowBuilder
{
    public static List<TreeRow> Build(NodeIndex index, Func<int, bool> isExpanded, Func<int, int> limitOf, int rootId = 0)
    {
        var rows = new List<TreeRow>();
        Append(rows, index, rootId, index.DepthOf(rootId), isExpanded, limitOf);
        return rows;
    }

    private static void Append(List<TreeRow> rows, NodeIndex index, int id, int depth, Func<int, bool> isExpanded, Func<int, int> limitOf)
    {
        var node = index.ById(id);
        var hasChildren = NodeChildren.HasChildren(node);
        var expanded = hasChildren && isExpanded(id);
        rows.Add(new TreeRow(id, depth, hasChildren, expanded, 0));
        if (!expanded)
            return;

        var limit = limitOf(id);
        var count = NodeChildren.Count(node);
        var shown = 0;
        foreach (var child in NodeChildren.Of(node))
        {
            if (shown >= limit)
                break;
            Append(rows, index, index.IdOf(child), depth + 1, isExpanded, limitOf);
            shown++;
        }
        if (count > shown)
            rows.Add(new TreeRow(id, depth + 1, false, false, count - shown));
    }

    /// <summary><paramref name="id"/> の行のインデックス。無ければ -1。</summary>
    public static int IndexOf(List<TreeRow> rows, int id)
    {
        for (var i = 0; i < rows.Count; i++)
            if (rows[i].Id == id && !rows[i].IsMore)
                return i;
        return -1;
    }
}
