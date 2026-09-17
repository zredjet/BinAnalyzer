namespace BinAnalyzer.Core.Interfaces;

/// <summary>
/// フォーマット定義の <c>imports:</c> を取得する手段（ファイル / HTTP / 埋め込みリソースなど）の抽象。
/// ローダーはインポート先の識別子の解決と内容の取得だけをリゾルバに委ね、循環検出とマージは自身で行う。
/// </summary>
public interface IImportResolver
{
    /// <summary>
    /// インポート元 <paramref name="basePath"/> から見た相対パス <paramref name="importPath"/> を
    /// 正規化した識別子（絶対パス、URL など）に解決する。同じファイルが常に同じ識別子になること（循環検出の基準）。
    /// </summary>
    string Resolve(string basePath, string importPath);

    /// <summary>
    /// <see cref="Resolve"/> が返した識別子の YAML テキストを取得する。見つからなければ <c>null</c>。
    /// </summary>
    Task<string?> ReadAsync(string resolvedPath);
}
