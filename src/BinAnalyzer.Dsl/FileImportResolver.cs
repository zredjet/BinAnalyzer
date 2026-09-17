using BinAnalyzer.Core.Interfaces;

namespace BinAnalyzer.Dsl;

/// <summary>
/// 既定のインポートリゾルバ。インポート元ファイルのディレクトリからの相対パスをファイルシステムで解決する
/// （<see cref="YamlFormatLoader.Load(string)"/> の従来挙動）。読み取りは同期的に完了するので、
/// 同期 API から <c>GetAwaiter().GetResult()</c> で待っても安全。
/// </summary>
public sealed class FileImportResolver : IImportResolver
{
    public static FileImportResolver Instance { get; } = new();

    public string Resolve(string basePath, string importPath)
    {
        var baseDir = Path.GetDirectoryName(Path.GetFullPath(basePath)) ?? "";
        return Path.GetFullPath(Path.Combine(baseDir, importPath));
    }

    public Task<string?> ReadAsync(string resolvedPath)
        => Task.FromResult(File.Exists(resolvedPath) ? File.ReadAllText(resolvedPath) : null);
}
