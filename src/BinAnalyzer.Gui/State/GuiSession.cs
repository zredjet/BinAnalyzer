using BinAnalyzer.Core.Diff;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Engine;
using BinAnalyzer.Gui.Abstractions;

namespace BinAnalyzer.Gui.State;

public enum PaneKind { Structure, Definition, Diff }

/// <summary>2 タブの差分。<see cref="DifferentBytes"/> は同じオフセットで異なるバイトの数（長さの差を含む。REQ-156）。</summary>
public sealed record DiffState(GuiDocument Left, GuiDocument Right, DiffResult Result, long DifferentBytes);

/// <summary>大きさの確認待ちのファイル（REQ-177）。「開く」で <see cref="GuiSession.ConfirmLargeFileAsync"/>。</summary>
public sealed record PendingLargeFile(OpenedFile File, string? FormatFile, Endianness? Endian);

/// <summary>開いているタブ集合と、アクティブタブ・右ペイン・差分などセッション全体の状態。</summary>
public sealed class GuiSession
{
    private readonly List<GuiDocument> _documents = [];

    public IFormatCatalog Catalog { get; }
    public IFileSource Files { get; }

    public IReadOnlyList<GuiDocument> Documents => _documents;
    public GuiDocument? Active { get; private set; }
    public PaneKind Pane { get; private set; } = PaneKind.Structure;
    public DiffState? Diff { get; private set; }
    public bool LiveDecode { get; set; } = true;
    public bool ShowPicker { get; private set; } = true;
    public bool ShowComparePicker { get; private set; }

    /// <summary>フォーマットを検出できず、ユーザーの選択待ちのファイル。</summary>
    public OpenedFile? PendingFile { get; private set; }

    /// <summary>
    /// このバイト数を超えるファイルは開く前に確認する（null なら確認しない）。デコード結果はノードごとにオブジェクトを持つため
    /// ファイルの数十倍のメモリを使うことがあり、デスクトップにはファイルサイズの上限が無い。
    /// </summary>
    public long? LargeFileThreshold { get; set; }
    public const long DefaultLargeFileThreshold = 256L * 1024 * 1024;

    /// <summary>大きさの確認待ちのファイル。</summary>
    public PendingLargeFile? PendingLarge { get; private set; }
    public string? Message { get; private set; }

    public IReadOnlyList<FormatCatalogEntry> Formats { get; private set; } = [];

    public event Action? Changed;
    public event Action? HoverChanged;

    public GuiSession(IFormatCatalog catalog, IFileSource files)
    {
        Catalog = catalog;
        Files = files;
    }

    public async Task InitializeAsync()
    {
        if (Formats.Count == 0)
            Formats = await Catalog.ListAsync();
    }

    private void Raise() => Changed?.Invoke();

    /// <summary>
    /// ファイルを開いてタブにする。フォーマット未指定なら拡張子で検出し、できなければ選択待ちにする。
    /// <see cref="LargeFileThreshold"/> を超えるファイルは <paramref name="confirmedLarge"/> でない限り確認待ちにして null を返す。
    /// </summary>
    public async Task<GuiDocument?> OpenAsync(OpenedFile file, string? formatFile = null, Endianness? endian = null, bool confirmedLarge = false)
    {
        await InitializeAsync();
        Message = null;
        if (!confirmedLarge && LargeFileThreshold is { } threshold && file.Data.LongLength > threshold)
        {
            PendingLarge = new PendingLargeFile(file, formatFile, endian);
            ShowPicker = true;
            Raise();
            return null;
        }
        PendingLarge = null;
        if (formatFile is null)
        {
            var ext = Path.GetExtension(file.Name);
            var detected = string.IsNullOrEmpty(ext) ? null : await Catalog.DetectByExtensionAsync(ext);
            formatFile = detected?.File;
        }
        if (formatFile is null)
        {
            PendingFile = file;
            Message = $"{file.Name}: フォーマットを自動検出できません。フォーマットを選択してください。";
            ShowPicker = false;
            Raise();
            return null;
        }

        FormatDocument format;
        try
        {
            format = await Catalog.LoadAsync(formatFile);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[gui] format load failed: {ex}");
            PendingFile = file;
            Message = $"フォーマット定義の読み込みに失敗: {ex.Message}";
            Raise();
            return null;
        }

        var doc = new GuiDocument(file.Name, file.Data, format, file.FullPath, endian);
        Attach(doc);
        _documents.Add(doc);
        Active = doc;
        PendingFile = null;
        ShowPicker = false;
        Pane = PaneKind.Structure;
        Raise();
        return doc;
    }

    /// <summary>選択待ちファイルに対してフォーマットを指定して開く（大きさの確認は済んでいる）。</summary>
    public async Task<GuiDocument?> OpenPendingAsync(string formatFile)
    {
        if (PendingFile is null) return null;
        var file = PendingFile;
        return await OpenAsync(file, formatFile, confirmedLarge: true);
    }

    /// <summary>大きさの確認待ちのファイルを開く。</summary>
    public async Task<GuiDocument?> ConfirmLargeFileAsync()
    {
        if (PendingLarge is not { } pending) return null;
        PendingLarge = null;
        return await OpenAsync(pending.File, pending.FormatFile, pending.Endian, confirmedLarge: true);
    }

    /// <summary>大きさの確認待ちのファイルを開かずに捨てる。</summary>
    public void CancelLargeFile()
    {
        PendingLarge = null;
        if (Active is not null) ShowPicker = false;
        Raise();
    }

    private void Attach(GuiDocument doc)
    {
        doc.Changed += Raise;
        doc.HoverChanged += () => HoverChanged?.Invoke();
        // 編集で Root が作り直されるので、そのタブを含む差分は無効にする
        doc.DataChanged += () => InvalidateDiffFor(doc);
    }

    public void Undo() => Active?.Undo();
    public void Redo() => Active?.Redo();

    /// <summary>
    /// 編集後のバイト列を保存する。<paramref name="chooseLocation"/> が true なら「名前を付けて保存」。
    /// 保存先が無い（標準入力から開いた等）場合もダイアログを出す。成功なら true、キャンセル / 失敗なら false。
    /// </summary>
    public async Task<bool> SaveAsync(GuiDocument doc, bool chooseLocation = false)
    {
        if (!_documents.Contains(doc)) return false;
        var file = new OpenedFile(doc.DisplayName, doc.Data, doc.FullPath);
        OpenedFile? saved;
        try
        {
            saved = await Files.SaveAsync(file, doc.Data, chooseLocation || doc.FullPath is null);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[gui] save failed: {ex}");
            Message = $"保存に失敗しました: {ex.Message}";
            Raise();
            return false;
        }
        if (saved is null) return false;
        doc.MarkSaved(saved);
        Message = $"保存しました: {saved.FullPath ?? saved.Name}";
        Raise();
        return true;
    }

    public void Activate(GuiDocument doc)
    {
        if (!_documents.Contains(doc)) return;
        Active = doc;
        ShowPicker = false;
        Raise();
    }

    public void Close(GuiDocument doc)
    {
        var i = _documents.IndexOf(doc);
        if (i < 0) return;
        _documents.RemoveAt(i);
        doc.Changed -= Raise;
        if (Diff is not null && (Diff.Left == doc || Diff.Right == doc))
            ClearDiffCore();
        if (Active == doc)
            Active = _documents.Count == 0 ? null : _documents[Math.Min(i, _documents.Count - 1)];
        if (Active is null) ShowPicker = true;
        Raise();
    }

    public void RequestPicker()
    {
        ShowPicker = true;
        Raise();
    }

    public void CancelPicker()
    {
        if (Active is not null) ShowPicker = false;
        Raise();
    }

    public void SetPane(PaneKind pane)
    {
        if (pane == PaneKind.Diff && Diff is null) return;
        Pane = pane;
        Raise();
    }

    /// <summary>定義ビューの上部に定義の診断の一覧を開いているか（REQ-185）。</summary>
    public bool DefinitionDiagnosticsOpen { get; private set; }

    /// <summary>右ペインを「定義」にして診断の一覧を開く（ステータスバーの件数から）。</summary>
    public void ShowDefinitionDiagnostics()
    {
        Pane = PaneKind.Definition;
        DefinitionDiagnosticsOpen = true;
        Raise();
    }

    public void ToggleDefinitionDiagnostics()
    {
        DefinitionDiagnosticsOpen = !DefinitionDiagnosticsOpen;
        Raise();
    }

    public async Task ChangeFormatAsync(GuiDocument doc, string formatFile)
    {
        if (doc.Format.File == formatFile) return;
        var format = await Catalog.LoadAsync(formatFile);
        InvalidateDiffFor(doc);
        doc.Redecode(format, keepEndian: true);
    }

    public void ChangeEndian(GuiDocument doc, Endianness? endian)
    {
        if (doc.EndianOverride == endian) return;
        InvalidateDiffFor(doc);
        doc.Redecode(endianOverride: endian);
    }

    private void InvalidateDiffFor(GuiDocument doc)
    {
        if (Diff is not null && (Diff.Left == doc || Diff.Right == doc))
            ClearDiffCore();
    }

    private void ClearDiffCore()
    {
        if (Diff is { } old)
        {
            old.Left.SetCompareData(null);
            old.Right.SetCompareData(null);
        }
        Diff = null;
        if (Pane == PaneKind.Diff) Pane = PaneKind.Structure;
    }

    /// <summary>差分を作り、左右のヘックスに相手のバイト列を渡す（変わったバイトの強調。REQ-156）。</summary>
    private void SetDiff(GuiDocument left, GuiDocument right)
    {
        Diff = new DiffState(left, right, DiffEngine.Compare(left.Root, right.Root), ByteDiff.CountDifferences(left.Data, right.Data));
        left.SetCompareData(right.Data);
        right.SetCompareData(left.Data);
    }

    public void ClearDiff()
    {
        ClearDiffCore();
        Raise();
    }

    public void ToggleComparePicker()
    {
        ShowComparePicker = !ShowComparePicker;
        Raise();
    }

    /// <summary>アクティブタブを左、<paramref name="other"/> を右として構造差分を取る。</summary>
    public void Compare(GuiDocument other)
    {
        if (Active is null || other == Active) return;
        SetDiff(Active, other);
        Pane = PaneKind.Diff;
        ShowComparePicker = false;
        Raise();
    }

    public void SwapDiff()
    {
        if (Diff is null) return;
        SetDiff(Diff.Right, Diff.Left);
        Raise();
    }

    public async Task PickAndOpenAsync()
    {
        var file = await Files.PickAsync();
        if (file is not null) await OpenAsync(file);
    }

    /// <summary>別のファイルを開き（同じフォーマット）、アクティブタブと比較する。</summary>
    public async Task PickAndCompareAsync()
    {
        var left = Active;
        if (left is null) return;
        var file = await Files.PickAsync();
        if (file is null) return;
        var right = await OpenAsync(file, left.Format.File, left.EndianOverride);
        if (right is null) return;
        Active = left;
        Compare(right);
    }

    public void SetMessage(string? message)
    {
        Message = message;
        Raise();
    }

    public void SetLiveDecode(bool on)
    {
        LiveDecode = on;
        Raise();
    }
}
