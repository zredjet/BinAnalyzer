using System.Runtime.CompilerServices;
using System.Text;
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
/// デコード結果ツリーの索引。ノード ID・親・種別・バイト→葉の逆引きを 1 回の走査で構築する。
/// 圧縮ノードの展開内容は「ストリーム空間」のオフセットを持つため、バイト逆引き（<see cref="Leaves"/>）には含めない。
/// </summary>
/// <remarks>
/// パス（<c>chunks[0].data.width</c>）と祖先 ID 列（<c>/0/2/3/</c>）はノード数に比例したメモリを食うので保持せず、
/// 親リンクをたどって必要なときに組み立てる（REQ-177）。ID は深さ優先の前順なので、親の ID は常に子より小さい。
/// </remarks>
public sealed class NodeIndex
{
    private readonly List<DecodedNode> _nodes;
    private readonly List<int> _parents;
    /// <summary>親の子列（padding 除外）の中での位置。配列要素なら要素インデックス。</summary>
    private readonly List<int> _ordinals;
    private readonly List<FieldKind> _kinds;
    private readonly List<bool> _inFileSpace;
    private readonly List<int> _depths;
    private readonly Dictionary<DecodedNode, int> _ids;
    private readonly Dictionary<string, FieldKindMapper.NameTraits> _nameTraits = new(StringComparer.Ordinal);
    private LeafSpan[] _leaves = [];

    public DecodedStruct Root { get; }
    public int Count => _nodes.Count;
    public int FieldCount { get; private set; }
    public IReadOnlyList<LeafSpan> Leaves => _leaves;

    private NodeIndex(DecodedStruct root, int capacity)
    {
        Root = root;
        _nodes = new List<DecodedNode>(capacity);
        _parents = new List<int>(capacity);
        _ordinals = new List<int>(capacity);
        _kinds = new List<FieldKind>(capacity);
        _inFileSpace = new List<bool>(capacity);
        _depths = new List<int>(capacity);
        _ids = new Dictionary<DecodedNode, int>(capacity, ReferenceEqualityComparer.Instance);
    }

    public static NodeIndex Build(DecodedStruct root)
    {
        // 先に数えて確保しておくと、リストと辞書の拡張（再ハッシュ）を避けられる。数える走査は割り当てが無く索引構築より一桁速い
        var index = new NodeIndex(root, CountNodes(root));
        index.Add(root, parent: -1, ordinal: 0, inFileSpace: true, depth: 0);
        index._leaves = index.CollectLeaves();
        return index;
    }

    // 「開く」は 1 回きりの冷えた呼び出しなので、階層化 JIT の未最適化コードで 100 万回まわらないよう最初から最適化する
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static int CountNodes(DecodedNode node)
    {
        var n = 1;
        switch (node)
        {
            case DecodedStruct s:
                foreach (var c in s.Children)
                    if (!c.IsPadding) n += CountNodes(c);
                break;
            case DecodedArray a:
                foreach (var e in a.Elements)
                    n += CountNodes(e);
                break;
            case DecodedCompressed { DecodedContent: { } content }:
                foreach (var c in content.Children)
                    if (!c.IsPadding) n += CountNodes(c);
                break;
        }
        return n;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void Add(DecodedNode node, int parent, int ordinal, bool inFileSpace, int depth)
    {
        var id = _nodes.Count;
        var parentNode = parent >= 0 ? _nodes[parent] : null;
        _nodes.Add(node);
        _parents.Add(parent);
        _ordinals.Add(ordinal);
        _kinds.Add(FieldKindMapper.Map(node, parentNode, _nameTraits));
        _inFileSpace.Add(inFileSpace);
        _depths.Add(depth);
        _ids[node] = id;
        if (parent >= 0 && !node.IsPadding)
            FieldCount++;

        var childInFileSpace = inFileSpace && node is not DecodedCompressed;
        switch (node)
        {
            case DecodedStruct s:
                AddStructChildren(s.Children, id, childInFileSpace, depth + 1);
                break;
            case DecodedArray a:
                for (var i = 0; i < a.Elements.Count; i++)
                    Add(a.Elements[i], id, i, childInFileSpace, depth + 1);
                break;
            case DecodedCompressed { DecodedContent: { } content }:
                AddStructChildren(content.Children, id, childInFileSpace, depth + 1);
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void AddStructChildren(IReadOnlyList<DecodedNode> children, int parent, bool inFileSpace, int depth)
    {
        var ordinal = 0;
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child.IsPadding)
                continue;
            Add(child, parent, ordinal++, inFileSpace, depth);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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
    public FieldKind KindOf(int id) => _kinds[id];
    public FieldKind KindOf(DecodedNode node) => _kinds[IdOf(node)];
    public int DepthOf(int id) => _depths[id];
    /// <summary>ノードがファイル上のバイト範囲を持つか（圧縮展開内容は false）。</summary>
    public bool IsInFileSpace(int id) => _inFileSpace[id];
    public IEnumerable<DecodedNode> Nodes => _nodes;

    /// <summary>親の子列（padding 除外）の中での位置。ルートは 0。</summary>
    public int OrdinalOf(int id) => _ordinals[id];

    // ---------------- パス ----------------

    public string PathOf(DecodedNode node) => PathOf(IdOf(node));

    /// <summary>ルートからのパス（<c>chunks[0].data.width</c>）。ルートは空文字。DiffEngine のパス表記と同じ。</summary>
    public string PathOf(int id)
    {
        if (id <= 0)
            return "";
        var sb = new StringBuilder();
        AppendPath(sb, id);
        return sb.ToString();
    }

    private void AppendPath(StringBuilder sb, int id)
    {
        var parent = _parents[id];
        if (parent > 0)
            AppendPath(sb, parent);
        if (_nodes[parent] is DecodedArray)
        {
            sb.Append('[').Append(_ordinals[id]).Append(']');
        }
        else
        {
            if (parent > 0)
                sb.Append('.');
            sb.Append(_nodes[id].Name);
        }
    }

    /// <summary>
    /// パスからノードを引く。同名の子が複数ある場合は最初のもの。ルートは空文字。存在しなければ null。
    /// </summary>
    public DecodedNode? ByPath(string path)
    {
        var id = IdByPath(path);
        return id >= 0 ? _nodes[id] : null;
    }

    /// <summary><see cref="ByPath"/> の ID 版。存在しなければ -1。</summary>
    public int IdByPath(string path)
    {
        if (path.Length == 0)
            return 0;
        var id = 0;
        var pos = 0;
        while (pos < path.Length)
        {
            if (path[pos] == '[')
            {
                var close = path.IndexOf(']', pos);
                if (close < 0 || !int.TryParse(path.AsSpan(pos + 1, close - pos - 1), out var elementIndex))
                    return -1;
                if (_nodes[id] is not DecodedArray)
                    return -1;
                id = ChildAt(id, elementIndex);
                pos = close + 1;
            }
            else
            {
                if (pos > 0)
                {
                    if (path[pos] != '.')
                        return -1;
                    pos++;
                }
                var end = pos;
                while (end < path.Length && path[end] != '.' && path[end] != '[')
                    end++;
                if (end == pos)
                    return -1;
                id = ChildByName(id, path.AsSpan(pos, end - pos));
                pos = end;
            }
            if (id < 0)
                return -1;
        }
        return id;
    }

    /// <summary>親 <paramref name="parent"/> の子（padding 除外）で <paramref name="ordinal"/> 番目の ID。</summary>
    private int ChildAt(int parent, int ordinal)
    {
        if (ordinal < 0)
            return -1;
        var node = _nodes[parent];
        DecodedNode? child = node switch
        {
            DecodedArray a => ordinal < a.Elements.Count ? a.Elements[ordinal] : null,
            _ => NodeChildren.Of(node).ElementAtOrDefault(ordinal),
        };
        return child is null ? -1 : IdOf(child);
    }

    private int ChildByName(int parent, ReadOnlySpan<char> name)
    {
        var node = _nodes[parent];
        if (node is DecodedArray)
            return -1;
        foreach (var child in NodeChildren.Of(node))
            if (name.SequenceEqual(child.Name))
                return IdOf(child);
        return -1;
    }

    // ---------------- 祖先 ----------------

    /// <summary>ルートから自身までの ID を <c>/0/3/7/</c> 形式で連結した文字列。CSS の属性部分一致に使う。</summary>
    public string AncestorIdPath(int id)
    {
        Span<int> chain = stackalloc int[Math.Min(_depths[id] + 1, 64)];
        var sb = new StringBuilder(_depths[id] * 4 + 4);
        sb.Append('/');
        AppendAncestorIds(sb, id, chain);
        return sb.ToString();
    }

    private void AppendAncestorIds(StringBuilder sb, int id, Span<int> chain)
    {
        // 深さが固定バッファを超える極端なツリーでは再帰で組み立てる
        if (_depths[id] + 1 > chain.Length)
        {
            if (id > 0)
                AppendAncestorIds(sb, _parents[id], chain);
            sb.Append(id).Append('/');
            return;
        }
        var n = 0;
        for (var cur = id; cur >= 0; cur = _parents[cur])
            chain[n++] = cur;
        for (var i = n - 1; i >= 0; i--)
            sb.Append(chain[i]).Append('/');
    }

    public bool IsAncestorOrSelf(int ancestor, int id)
    {
        if (ancestor < 0 || id < 0)
            return false;
        // 前順 ID なので祖先は必ず自身以下の ID
        for (var cur = id; cur >= ancestor; cur = _parents[cur])
        {
            if (cur == ancestor)
                return true;
            if (cur == 0)
                break;
        }
        return false;
    }

    public bool IsAncestorOrSelf(DecodedNode ancestor, DecodedNode node)
        => IsAncestorOrSelf(IdOf(ancestor), IdOf(node));

    /// <summary>配列要素なら親配列内のインデックス、そうでなければ null。</summary>
    public int? ElementIndexOf(int id)
    {
        if (id <= 0 || _nodes[_parents[id]] is not DecodedArray)
            return null;
        return _ordinals[id];
    }

    // ---------------- 葉（バイト逆引き） ----------------

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

    // ---------------- 検索 ----------------

    /// <summary>パスパターン（<c>**.width</c> 等、<see cref="PathFilter"/> 構文）に一致するノードを文書順で返す。</summary>
    public List<DecodedNode> FindByPathPattern(string pattern)
    {
        var filter = new PathFilter([pattern]);
        var results = new List<DecodedNode>();
        // 前順 ID なので、親のパスは常に先に確定している。深さごとに StringBuilder を使い回して全パスを 1 回の走査で作る
        var paths = new string[_nodes.Count];
        paths[0] = "";
        for (var id = 1; id < _nodes.Count; id++)
        {
            var parent = _parents[id];
            var parentPath = paths[parent];
            paths[id] = _nodes[parent] is DecodedArray
                ? $"{parentPath}[{_ordinals[id]}]"
                : parentPath.Length == 0 ? _nodes[id].Name : $"{parentPath}.{_nodes[id].Name}";
            if (filter.Matches(paths[id]))
                results.Add(_nodes[id]);
        }
        return results;
    }
}
