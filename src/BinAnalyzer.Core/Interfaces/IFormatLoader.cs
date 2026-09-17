using BinAnalyzer.Core.Models;

namespace BinAnalyzer.Core.Interfaces;

public interface IFormatLoader
{
    FormatDefinition Load(string path);
    FormatDefinition LoadFromString(string yaml);

    /// <summary>
    /// YAML テキストを読み、<c>imports:</c> を <paramref name="resolver"/> 経由で解決する。
    /// <paramref name="basePath"/> は YAML 自身の識別子（リゾルバが解釈できる形式）で、相対インポートの基準になる。
    /// </summary>
    Task<FormatDefinition> LoadAsync(string yaml, string basePath, IImportResolver resolver);
}
