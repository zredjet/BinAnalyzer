using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Gui.Abstractions;
using BinAnalyzer.Presentation;

namespace BinAnalyzer.Gui.State;

/// <summary>タブ 1 枚分の状態: バイナリ、フォーマット、デコード結果と索引、選択・ホバー・展開・検索。</summary>
public sealed class GuiDocument
{
    public string DisplayName { get; }
    public string? FullPath { get; }
    public byte[] Data { get; }
    public FormatDocument Format { get; private set; }
    public Endianness? EndianOverride { get; private set; }

    public DecodedStruct Root { get; private set; } = null!;
    public NodeIndex Index { get; private set; } = null!;
    public HexRowBuilder Hex { get; private set; } = null!;
    public StructureMap Map { get; private set; } = null!;
    public ChecksumSummary Summary { get; private set; } = null!;
    public IReadOnlyList<DecodeError> Errors { get; private set; } = [];
    public TimeSpan DecodeTime { get; private set; }
    public string? DecodeFailure { get; private set; }

    public int SelectedId { get; private set; } = -1;
    public int HoveredId { get; private set; } = -1;
    public DecodedNode? Selected => Index.TryById(SelectedId);
    public HashSet<int> Expanded { get; } = [];

    public string SearchQuery { get; private set; } = "";
    public IReadOnlyList<DecodedNode> SearchResults { get; private set; } = [];
    public int SearchIndex { get; private set; } = -1;

    /// <summary>選択・展開・再デコードなど描画に影響する変更。</summary>
    public event Action? Changed;
    /// <summary>ホバーのみ（軽量な再描画用）。</summary>
    public event Action? HoverChanged;

    public GuiDocument(string displayName, byte[] data, FormatDocument format, string? fullPath = null, Endianness? endianOverride = null)
    {
        DisplayName = displayName;
        FullPath = fullPath;
        Data = data;
        Format = format;
        EndianOverride = endianOverride;
        Apply(DecodeService.Decode(data, format.Definition, endianOverride));
        Expanded.Add(0);
        foreach (var child in NodeChildren.Of(Root))
            Expanded.Add(Index.IdOf(child));
    }

    private void Apply(DecodeOutcome outcome)
    {
        Root = outcome.Root;
        Errors = outcome.Errors;
        DecodeTime = outcome.Elapsed;
        DecodeFailure = outcome.Failure;
        Index = NodeIndex.Build(Root);
        Hex = new HexRowBuilder(Data, Index);
        Map = StructureMapBuilder.Build(Index, Data.Length);
        Summary = ChecksumSummary.Compute(Index);
    }

    /// <summary>フォーマット / エンディアンを変えて再デコードする。選択はパスで復元する。</summary>
    public void Redecode(FormatDocument? format = null, Endianness? endianOverride = null, bool keepEndian = false)
    {
        var selectedPath = SelectedId >= 0 ? Index.PathOf(SelectedId) : null;
        var expandedPaths = Expanded.Select(id => Index.PathOf(id)).ToList();
        Format = format ?? Format;
        EndianOverride = keepEndian ? EndianOverride : endianOverride;
        Apply(DecodeService.Decode(Data, Format.Definition, EndianOverride));
        Expanded.Clear();
        Expanded.Add(0);
        foreach (var p in expandedPaths)
            if (Index.ByPath(p) is { } n) Expanded.Add(Index.IdOf(n));
        SelectedId = selectedPath is not null && Index.ByPath(selectedPath) is { } sel ? Index.IdOf(sel) : -1;
        HoveredId = -1;
        SearchResults = [];
        SearchIndex = -1;
        Changed?.Invoke();
    }

    public void Select(DecodedNode? node) => Select(node is null ? -1 : Index.IdOf(node));

    public void Select(int id)
    {
        if (id >= Index.Count) id = -1;
        if (id >= 0) ExpandTo(id, raise: false);
        if (SelectedId == id) { Changed?.Invoke(); return; }
        SelectedId = id;
        Changed?.Invoke();
    }

    public void Hover(int id)
    {
        if (HoveredId == id) return;
        HoveredId = id;
        HoverChanged?.Invoke();
    }

    public bool IsExpanded(int id) => Expanded.Contains(id);

    public void ToggleExpanded(int id)
    {
        if (!Expanded.Remove(id)) Expanded.Add(id);
        Changed?.Invoke();
    }

    /// <summary>指定ノードの祖先を全て展開する。</summary>
    public void ExpandTo(int id, bool raise = true)
    {
        for (var p = Index.ParentIdOf(id); p >= 0; p = Index.ParentIdOf(p))
            Expanded.Add(p);
        if (raise) Changed?.Invoke();
    }

    public void ExpandAll()
    {
        for (var id = 0; id < Index.Count; id++)
            if (NodeChildren.HasChildren(Index.ById(id))) Expanded.Add(id);
        Changed?.Invoke();
    }

    public void CollapseAll()
    {
        Expanded.Clear();
        Expanded.Add(0);
        Changed?.Invoke();
    }

    /// <summary>選択ノードのファイル上の範囲 [Offset, End)。未選択・ストリーム空間なら (-1, -1)。</summary>
    public (long Offset, long End) SelectedRange
    {
        get
        {
            if (SelectedId < 0 || !Index.IsInFileSpace(SelectedId)) return (-1, -1);
            var n = Index.ById(SelectedId);
            return (n.Offset, n.Offset + n.Size);
        }
    }

    /// <summary>
    /// 検索。<c>*</c> / <c>.</c> / <c>[</c> を含むならパスパターン（<c>**.width</c>）、それ以外は名前の部分一致。
    /// 最初の一致を選択する。
    /// </summary>
    public void Search(string query)
    {
        SearchQuery = query;
        if (string.IsNullOrWhiteSpace(query))
        {
            SearchResults = [];
            SearchIndex = -1;
            Changed?.Invoke();
            return;
        }
        SearchResults = query.IndexOfAny(['*', '.', '[']) >= 0
            ? Index.FindByPathPattern(query)
            : NodeSearch.ByName(Root, query);
        SearchIndex = SearchResults.Count > 0 ? 0 : -1;
        if (SearchIndex >= 0) Select(SearchResults[0]); else Changed?.Invoke();
    }

    public void NextMatch() => StepMatch(+1);
    public void PreviousMatch() => StepMatch(-1);

    private void StepMatch(int delta)
    {
        if (SearchResults.Count == 0) return;
        SearchIndex = (SearchIndex + delta + SearchResults.Count) % SearchResults.Count;
        Select(SearchResults[SearchIndex]);
    }
}
