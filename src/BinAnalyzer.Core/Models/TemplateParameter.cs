namespace BinAnalyzer.Core.Models;

/// <summary>
/// テンプレート構造体のパラメータ定義。
/// DefaultValueがnullの場合は必須パラメータ。
/// </summary>
public sealed record TemplateParameter(string Name, long? DefaultValue);
