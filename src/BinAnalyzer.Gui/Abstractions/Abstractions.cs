using BinAnalyzer.Core.Models;

namespace BinAnalyzer.Gui.Abstractions;

/// <summary>フォーマットカタログの 1 エントリ。</summary>
public sealed record FormatCatalogEntry(string Name, string File, IReadOnlyList<string> Extensions);

/// <summary>読み込んだフォーマット定義（IR と元 YAML テキスト）。</summary>
public sealed record FormatDocument(string File, string DisplayName, FormatDefinition Definition, string YamlText);

/// <summary>ホストから渡されたバイナリファイル。</summary>
public sealed record OpenedFile(string Name, byte[] Data, string? FullPath = null);

/// <summary>フォーマット定義の供給元（Web: HTTP、デスクトップ: ファイルシステム）。</summary>
public interface IFormatCatalog
{
    Task<IReadOnlyList<FormatCatalogEntry>> ListAsync();
    Task<FormatDocument> LoadAsync(string file);
    Task<FormatCatalogEntry?> DetectByExtensionAsync(string extension);

    /// <summary>
    /// 定義元ファイル（<see cref="Core.Models.StructDefinition.SourceFile"/> の識別子）の YAML テキスト。
    /// インポートされた struct の定義を定義ビューに出すために使う（REQ-172）。取得できなければ null。
    /// </summary>
    Task<string?> ReadSourceAsync(string sourceFile);
}

/// <summary>バイナリファイルの取得手段（デスクトップ: ネイティブダイアログ、Web: InputFile）。</summary>
public interface IFileSource
{
    /// <summary>true ならホストがファイルダイアログを提供する。false なら UI 側で InputFile / ドロップを使う。</summary>
    bool SupportsNativePicker { get; }
    Task<OpenedFile?> PickAsync();

    /// <summary>
    /// 編集後のバイト列を保存する。<paramref name="chooseLocation"/> が true か <c>file.FullPath</c> が無ければ保存先を選ばせる
    /// （デスクトップ: ネイティブダイアログ、Web: ダウンロード）。保存したファイルの名前・パスを返し、キャンセルなら null。
    /// </summary>
    Task<OpenedFile?> SaveAsync(OpenedFile file, byte[] data, bool chooseLocation);
}
