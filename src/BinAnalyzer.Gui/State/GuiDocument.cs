using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Patching;
using BinAnalyzer.Engine;
using BinAnalyzer.Gui.Abstractions;
using BinAnalyzer.Presentation;

namespace BinAnalyzer.Gui.State;

/// <summary>タブ 1 枚分の状態: バイナリ、フォーマット、デコード結果と索引、選択・ホバー・展開・検索、編集履歴。</summary>
public sealed class GuiDocument
{
    public string DisplayName { get; private set; }
    public string? FullPath { get; private set; }

    /// <summary>読み込んだ原本。編集しても変わらない。</summary>
    public byte[] OriginalData { get; }

    /// <summary>
    /// 現在デコード・表示しているバイト列。編集のたびに新しい配列に差し替える（配列自体は書き換えない）ので、
    /// 参照を保持している側は <see cref="Revision"/> で世代を判定できる。
    /// </summary>
    public byte[] Data { get; private set; }

    /// <summary>編集・Undo / Redo のたびに増える世代番号。</summary>
    public int Revision { get; private set; }

    public FormatDocument Format { get; private set; }

    /// <summary>差分表示中の相手ファイルのバイト列（REQ-156）。ヘックスで同じオフセットの異なるバイトを強調する。差分表示中でなければ null。</summary>
    public byte[]? CompareData { get; private set; }
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

    private readonly Stack<EditRecord> _undo = new();
    private readonly Stack<EditRecord> _redo = new();
    private EditRecord? _savedMarker;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    /// <summary>最後に保存（または読み込み）した状態から変更があるか。</summary>
    public bool IsDirty => !ReferenceEquals(_undo.Count > 0 ? _undo.Peek() : null, _savedMarker);
    public int UndoCount => _undo.Count;
    /// <summary>直近に適用した編集（インスペクターの結果表示用）。Undo / Redo / 選択変更で null になる。</summary>
    public EditRecord? LastEdit { get; private set; }

    /// <summary>選択・展開・再デコードなど描画に影響する変更。</summary>
    public event Action? Changed;
    /// <summary>ホバーのみ（軽量な再描画用）。</summary>
    public event Action? HoverChanged;
    /// <summary>バイト列が変わった（編集 / Undo / Redo）。<see cref="Changed"/> より先に発火する。</summary>
    public event Action? DataChanged;

    public GuiDocument(string displayName, byte[] data, FormatDocument format, string? fullPath = null, Endianness? endianOverride = null)
    {
        DisplayName = displayName;
        FullPath = fullPath;
        OriginalData = data;
        Data = data;
        Format = format;
        EndianOverride = endianOverride;
        Apply(DecodeService.Decode(data, format.Definition, endianOverride));
        Expanded.Add(0);
        foreach (var child in NodeChildren.Of(Root))
            Expanded.Add(Index.IdOf(child));
        // デコードと索引構築はノード数に比例したゴミ（ファイルの数倍）を出す。開いた直後に一度だけ回収して
        // 常駐メモリを木と索引のぶんに近づける（REQ-180）。編集のたびの再デコードでは行わない
        GC.Collect();
    }

    private void Apply(DecodeOutcome outcome)
    {
        Root = outcome.Root;
        Errors = outcome.Errors;
        DecodeTime = outcome.Elapsed;
        DecodeFailure = outcome.Failure;
        var t0 = GuiTiming.Now;
        Index = NodeIndex.Build(Root);
        var t1 = GuiTiming.Now;
        Hex = NewHex();
        Map = StructureMapBuilder.Build(Index, Data.Length);
        var t2 = GuiTiming.Now;
        Summary = ChecksumSummary.Compute(Index);
        var t3 = GuiTiming.Now;
        GuiTiming.Log($"{DisplayName}: decode {DecodeTime.TotalMilliseconds:F1} ms, index {t1 - t0:F1} ms ({Index.Count} nodes), map {t2 - t1:F1} ms, summary {t3 - t2:F1} ms, {Data.Length} bytes");
    }

    /// <summary>
    /// ヘックスの行生成器。比較相手が無ければ null を明示的に渡す（<c>byte[]</c> の null をそのまま渡すと
    /// 空の <see cref="ReadOnlyMemory{T}"/> に変換され、全バイトが「相手に無い＝差分」になる）。
    /// </summary>
    private HexRowBuilder NewHex() => CompareData is { } other
        ? new HexRowBuilder(Data, Index, other)
        : new HexRowBuilder(Data, Index);

    /// <summary>差分の相手のバイト列を設定する（null で解除）。ヘックスの行を作り直すだけで再デコードはしない。</summary>
    public void SetCompareData(byte[]? other)
    {
        if (ReferenceEquals(CompareData, other)) return;
        CompareData = other;
        Hex = NewHex();
        Changed?.Invoke();
    }

    /// <summary>フォーマット / エンディアンを変えて再デコードする。選択はパスで復元する。</summary>
    public void Redecode(FormatDocument? format = null, Endianness? endianOverride = null, bool keepEndian = false)
    {
        Format = format ?? Format;
        EndianOverride = keepEndian ? EndianOverride : endianOverride;
        RedecodePreservingSelection();
    }

    private void RedecodePreservingSelection()
    {
        var selectedPath = SelectedId >= 0 ? Index.PathOf(SelectedId) : null;
        var expandedPaths = Expanded.Select(id => Index.PathOf(id)).ToList();
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
        GuiTiming.MarkSelect();
        if (id >= Index.Count) id = -1;
        if (id >= 0) ExpandTo(id, raise: false);
        if (SelectedId == id) { Changed?.Invoke(); return; }
        SelectedId = id;
        LastEdit = null;
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

    // ---------------- 編集（REQ-169） ----------------

    private DecodeOptions PatchOptions => new() { Endianness = EndianOverride };

    /// <summary>入力中のプレビュー: 書き込みバイト・エラー・再計算されるチェックサム。</summary>
    public EditPreview PreviewEdit(int id, string input)
    {
        var editability = FieldEditability.Of(Index, id);
        if (!editability.CanEdit)
            return new EditPreview(editability, null, null, null, true, []);

        var node = Index.ById(id);
        var range = new ByteRange(node.Offset, node.Size);
        var deps = ChecksumDependencies.Covering(Index, range, excludeId: id);
        var result = FieldEncoder.Instance.Encode(node, input);
        if (!result.IsSuccess)
            return new EditPreview(editability, null, result.Error, null, false, deps);

        var unchanged = range.End <= Data.Length
            && Data.AsSpan((int)range.Offset, (int)range.Size).SequenceEqual(result.Bytes);
        return new EditPreview(editability, result.Bytes, null, result.Note, unchanged, deps);
    }

    /// <summary>編集欄の初期文字列（現在値）。</summary>
    public string EditText(int id) => FieldEncoder.Instance.InitialText(Index.ById(id));

    /// <summary>指定フィールドの現在のバイト列が原本と異なるか（「元に戻す」の可否）。</summary>
    public bool IsFieldModified(int id)
    {
        if (id <= 0 || id >= Index.Count || !Index.IsInFileSpace(id)) return false;
        var node = Index.ById(id);
        var range = new ByteRange(node.Offset, node.Size);
        if (range.IsEmpty || range.End > Data.Length || range.End > OriginalData.Length) return false;
        return !Data.AsSpan((int)range.Offset, (int)range.Size)
            .SequenceEqual(OriginalData.AsSpan((int)range.Offset, (int)range.Size));
    }

    /// <summary>入力値をエンコードして書き込み、依存チェックサムを再計算し、再デコードする。</summary>
    public EditOutcome ApplyEdit(int id, string input)
    {
        var preview = PreviewEdit(id, input);
        if (!preview.Editability.CanEdit)
            return EditOutcome.Fail(preview.Editability.Reason ?? "編集できないフィールドです");
        if (preview.Bytes is null)
            return EditOutcome.Fail(preview.Error ?? "値を解釈できません");
        if (preview.Unchanged)
            return new EditOutcome(true, null, null);

        var node = Index.ById(id);
        var path = Index.PathOf(id);
        var summary = $"{path} = {input.Trim()}";
        var record = ApplyPatches(path, summary, [new BytePatch(node.Offset, preview.Bytes)]);
        return new EditOutcome(true, null, record);
    }

    /// <summary>指定フィールドのバイト列を原本に戻す（依存チェックサムも再計算）。変更が無ければ何もしない。</summary>
    public EditRecord? RevertField(int id)
    {
        if (!IsFieldModified(id)) return null;
        var node = Index.ById(id);
        var original = OriginalData.AsSpan((int)node.Offset, (int)node.Size).ToArray();
        var path = Index.PathOf(id);
        return ApplyPatches(path, $"{path} を元に戻す", [new BytePatch(node.Offset, original)]);
    }

    private EditRecord ApplyPatches(string path, string summary, IReadOnlyList<BytePatch> patches)
    {
        var outcome = BinaryPatcher.Apply(Data, Format.Definition, patches, PatchOptions);
        var writes = new List<ByteWrite>(patches.Count + outcome.ChecksumFixes.Count);
        foreach (var p in patches)
            writes.Add(new ByteWrite(p.Offset, Data.AsSpan((int)p.Offset, p.Bytes.Length).ToArray(), p.Bytes));
        foreach (var f in outcome.ChecksumFixes)
            writes.Add(new ByteWrite(f.Range.Offset, f.Before, f.After));

        var record = new EditRecord(path, summary, writes, outcome.ChecksumFixes);
        _undo.Push(record);
        _redo.Clear();
        LastEdit = record;
        ReplaceData(outcome.Data);
        return record;
    }

    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        var record = _undo.Pop();
        _redo.Push(record);
        var buffer = (byte[])Data.Clone();
        for (var i = record.Writes.Count - 1; i >= 0; i--)
            record.Writes[i].Before.CopyTo(buffer, (int)record.Writes[i].Offset);
        LastEdit = null;
        ReplaceData(buffer);
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        var record = _redo.Pop();
        _undo.Push(record);
        var buffer = (byte[])Data.Clone();
        foreach (var w in record.Writes)
            w.After.CopyTo(buffer, (int)w.Offset);
        LastEdit = null;
        ReplaceData(buffer);
        return true;
    }

    private void ReplaceData(byte[] data)
    {
        Data = data;
        Revision++;
        DataChanged?.Invoke();
        RedecodePreservingSelection();
    }

    /// <summary>保存済みとしてマークする。名前を付けて保存した場合は表示名・パスも更新する。</summary>
    public void MarkSaved(OpenedFile saved)
    {
        _savedMarker = _undo.Count > 0 ? _undo.Peek() : null;
        if (!string.IsNullOrEmpty(saved.FullPath))
            FullPath = saved.FullPath;
        if (!string.IsNullOrEmpty(saved.Name))
            DisplayName = saved.Name;
        Changed?.Invoke();
    }
}
