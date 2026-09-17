using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Presentation;

public sealed record BreadcrumbItem(int NodeId, string Label);

public static class BreadcrumbBuilder
{
    /// <summary>ルート（<paramref name="rootLabel"/>）から <paramref name="node"/> までのチェーン。配列要素は <c>#i</c>。</summary>
    public static IReadOnlyList<BreadcrumbItem> Build(DecodedNode node, NodeIndex index, string rootLabel)
    {
        var items = new List<BreadcrumbItem>();
        for (var id = index.IdOf(node); id >= 0; id = index.ParentIdOf(id))
        {
            var n = index.ById(id);
            var label = id == 0 ? rootLabel
                : index.ElementIndexOf(id) is { } i ? $"#{i}"
                : n.Name;
            items.Add(new BreadcrumbItem(id, label));
        }
        items.Reverse();
        return items;
    }
}
