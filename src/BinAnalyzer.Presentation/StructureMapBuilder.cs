using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Presentation;

public sealed record MapBand(int NodeId, FieldKind Kind, long Size);
public sealed record MapSegment(int NodeId, string Label, long Offset, long Size, IReadOnlyList<MapBand> Bands);
public sealed record StructureMap(IReadOnlyList<MapSegment> Segments, long TotalSize);

/// <summary>フッターの構造マップ。ルート直下の子（配列は要素ごと）をセグメントに、その直下をバンドにする。</summary>
public static class StructureMapBuilder
{
    public const int MaxSegments = 256;

    public static StructureMap Build(NodeIndex index, long totalSize)
    {
        var segments = new List<MapSegment>();
        foreach (var child in NodeChildren.Of(index.Root))
        {
            if (child is DecodedArray array)
            {
                var firstElementSegment = segments.Count;
                foreach (var element in array.Elements)
                {
                    if (segments.Count - firstElementSegment >= MaxSegments)
                    {
                        var shown = segments.Count - firstElementSegment;
                        var remaining = array.Elements.Count - shown;
                        var lastEnd = segments[^1].Offset + segments[^1].Size;
                        var restSize = Math.Max(0, array.Offset + array.Size - lastEnd);
                        var arrayId = index.IdOf(array);
                        segments.Add(new MapSegment(arrayId, $"+{remaining}", lastEnd, restSize,
                            [new MapBand(arrayId, FieldKind.Array, restSize)]));
                        break;
                    }
                    segments.Add(Segment(element, index));
                }
            }
            else
            {
                segments.Add(Segment(child, index));
            }
        }
        return new StructureMap(segments, totalSize);
    }

    private static MapSegment Segment(DecodedNode node, NodeIndex index)
    {
        var id = index.IdOf(node);
        var children = NodeChildren.Of(node).ToList();
        var bands = children.Count == 0 || node is DecodedCompressed
            ? [new MapBand(id, index.KindOf(id), node.Size)]
            : children.Where(c => c.Size > 0).Select(c => new MapBand(index.IdOf(c), index.KindOf(c), c.Size)).ToList();
        return new MapSegment(id, LabelFor(node, children), node.Offset, node.Size, bands);
    }

    /// <summary>構造体の先頭付近にある短い文字列（チャンク種別など）をラベルにする。無ければノード名。</summary>
    private static string LabelFor(DecodedNode node, List<DecodedNode> children)
    {
        if (node is DecodedStruct)
        {
            var tag = children.Take(3).OfType<DecodedString>().FirstOrDefault(s => s.Value.Length is > 0 and <= 8);
            if (tag is not null)
                return tag.Value;
        }
        return node.Name;
    }
}
