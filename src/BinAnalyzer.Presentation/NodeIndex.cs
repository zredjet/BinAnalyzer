using BinAnalyzer.Core;
using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Presentation;

/// <summary>ファイル上のバイト範囲を占める葉ノード。</summary>
public readonly record struct LeafSpan(int Id, long Offset, long Size, FieldKind Kind)
{
    public long End => Offset + Size;
    public bool Contains(long offset) => offset >= Offset && offset < End;
}

/// <summary>
/// デコード結果ツリーの索引。ノード ID・親・パス・種別・バイト→葉の逆引きを 1 回の走査で構築する。
/// 圧縮ノードの展開内容は「ストリーム空間」のオフセットを持つため、バイト逆引き（<see cref="Leaves"/>）には含めない。
/// </summary>
public sealed class NodeIndex
{
    private readonly List<DecodedNode> _nodes = [];
    private readonly List<int> _parents = [];
    private readonly List<string> _paths = [];
    private readonly List<string> _ancestorPaths = [];
    private readonly List<FieldKind> _kinds = [];
    private readonly List<bool> _inFileSpace = [];
    private readonly List<int> _depths = [];
    private readonly Dictionary<DecodedNode, int> _ids = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, int> _byPath = new(StringComparer.Ordinal);
    private LeafSpan[] _leaves = [];

    public DecodedStruct Root { get; }
    public int Count => _nodes.Count;
    public int FieldCount { get; private set; }
    public IReadOnlyList<LeafSpan> Leaves => _leaves;

    private NodeIndex(DecodedStruct root)
    {
        Root = root;
    }

    public static NodeIndex Build(DecodedStruct root)
    {
        var index = new NodeIndex(root);
        index.Add(root, parent: -1, path: "", inFileSpace: true, depth: 0);
        index._leaves = index.CollectLeaves();
        return index;
    }

    private void Add(DecodedNode node, int parent, string path, bool inFileSpace, int depth)
    {
        var id = _nodes.Count;
        var parentNode = parent >= 0 ? _nodes[parent] : null;
        _nodes.Add(node);
        _parents.Add(parent);
        _paths.Add(path);
        _ancestorPaths.Add((parent >= 0 ? _ancestorPaths[parent] : "/") + id + "/");
        _kinds.Add(FieldKindMapper.Map(node, parentNode));
        _inFileSpace.Add(inFileSpace);
        _depths.Add(depth);
        _ids[node] = id;
        _byPath.TryAdd(path, id);
        if (parent >= 0 && !node.IsPadding)
            FieldCount++;

        var childInFileSpace = inFileSpace && node is not DecodedCompressed;
        var i = 0;
        foreach (var child in NodeChildren.Of(node))
        {
            var childPath = node is DecodedArray
                ? $"{path}[{i}]"
                : string.IsNullOrEmpty(path) ? child.Name : $"{path}.{child.Name}";
            Add(child, id, childPath, childInFileSpace, depth + 1);
            i++;
        }
    }

    private LeafSpan[] CollectLeaves()
    {
        var leaves = new List<LeafSpan>();
        for (var id = 1; id < _nodes.Count; id++)
        {
            var node = _nodes[id];
            if (!_inFileSpace[id] || node.Size <= 0)
                continue;
            // 圧縮ノードはファイル空間では葉として扱う（展開内容はストリーム空間）
            if (node is DecodedCompressed || !NodeChildren.HasChildren(node))
                leaves.Add(new LeafSpan(id, node.Offset, node.Size, _kinds[id]));
        }
        leaves.Sort((a, b) => a.Offset != b.Offset ? a.Offset.CompareTo(b.Offset) : a.Id.CompareTo(b.Id));
        return leaves.ToArray();
    }

    public int IdOf(DecodedNode node) => _ids.TryGetValue(node, out var id) ? id : -1;
    public DecodedNode ById(int id) => _nodes[id];
    public DecodedNode? TryById(int id) => id >= 0 && id < _nodes.Count ? _nodes[id] : null;
    public DecodedNode? ParentOf(DecodedNode node) => ParentOf(IdOf(node));
    public DecodedNode? ParentOf(int id) => id > 0 ? _nodes[_parents[id]] : null;
    public int ParentIdOf(int id) => id >= 0 ? _parents[id] : -1;
    public string PathOf(DecodedNode node) => PathOf(IdOf(node));
    public string PathOf(int id) => _paths[id];
    /// <summary>ルートから自身までの ID を <c>/0/3/7/</c> 形式で連結した文字列。CSS の属性部分一致に使う。</summary>
    public string AncestorIdPath(int id) => _ancestorPaths[id];
    public FieldKind KindOf(int id) => _kinds[id];
    public FieldKind KindOf(DecodedNode node) => _kinds[IdOf(node)];
    public int DepthOf(int id) => _depths[id];
    /// <summary>ノードがファイル上のバイト範囲を持つか（圧縮展開内容は false）。</summary>
    public bool IsInFileSpace(int id) => _inFileSpace[id];
    public DecodedNode? ByPath(string path) => _byPath.TryGetValue(path, out var id) ? _nodes[id] : null;
    public IEnumerable<DecodedNode> Nodes => _nodes;

    public bool IsAncestorOrSelf(int ancestor, int id)
        => _ancestorPaths[id].Contains("/" + ancestor + "/", StringComparison.Ordinal);

    public bool IsAncestorOrSelf(DecodedNode ancestor, DecodedNode node)
        => IsAncestorOrSelf(IdOf(ancestor), IdOf(node));

    /// <summary>配列要素なら親配列内のインデックス、そうでなければ null。</summary>
    public int? ElementIndexOf(int id)
    {
        if (id <= 0 || _nodes[_parents[id]] is not DecodedArray arr)
            return null;
        var node = _nodes[id];
        for (var i = 0; i < arr.Elements.Count; i++)
            if (ReferenceEquals(arr.Elements[i], node))
                return i;
        return null;
    }

    /// <summary>ファイルオフセットを含む葉。隙間なら null。同一オフセットの葉が複数ある場合は ID が最小のもの。</summary>
    public LeafSpan? LeafAt(long offset)
    {
        var i = LastLeafStartingAtOrBefore(offset);
        if (i < 0)
            return null;
        // 同じ Offset の先頭まで戻る
        while (i > 0 && _leaves[i - 1].Offset == _leaves[i].Offset)
            i--;
        // 同じ Offset のグループ内で最初に含むもの（通常は先頭）
        for (var j = i; j < _leaves.Length && _leaves[j].Offset == _leaves[i].Offset; j++)
            if (_leaves[j].Contains(offset))
                return _leaves[j];
        // 手前の葉が跨いでいる可能性（サイズが大きい葉の後にオフセット順で小さい葉は来ないので通常はここで終わり）
        return _leaves[i].Contains(offset) ? _leaves[i] : null;
    }

    /// <summary><paramref name="offset"/> 以上の Offset を持つ最初の葉のインデックス（無ければ Leaves.Count）。</summary>
    public int FirstLeafIndexAtOrAfter(long offset)
    {
        var lo = 0;
        var hi = _leaves.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (_leaves[mid].Offset < offset) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    private int LastLeafStartingAtOrBefore(long offset)
    {
        var i = FirstLeafIndexAtOrAfter(offset + 1) - 1;
        return i;
    }

    /// <summary>パスパターン（<c>**.width</c> 等、<see cref="PathFilter"/> 構文）に一致するノードを文書順で返す。</summary>
    public List<DecodedNode> FindByPathPattern(string pattern)
    {
        var filter = new PathFilter([pattern]);
        var results = new List<DecodedNode>();
        for (var id = 1; id < _nodes.Count; id++)
            if (filter.Matches(_paths[id]))
                results.Add(_nodes[id]);
        return results;
    }
}
