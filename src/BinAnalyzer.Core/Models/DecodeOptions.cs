namespace BinAnalyzer.Core.Models;

/// <summary>
/// デコード時のグローバルオプション。CLIオプション等から設定される。
/// </summary>
public sealed class DecodeOptions
{
    /// <summary>繰り返し回数のグローバルデフォルト上限。フィールドレベルの repeat_max が優先される。</summary>
    public int? MaxRepeat { get; init; }
}
