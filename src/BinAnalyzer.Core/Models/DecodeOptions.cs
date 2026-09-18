namespace BinAnalyzer.Core.Models;

/// <summary>
/// デコード時のグローバルオプション。CLIオプション等から設定される。
/// </summary>
public sealed class DecodeOptions
{
    /// <summary>繰り返し回数のグローバルデフォルト上限。フィールドレベルの repeat_max が優先される。</summary>
    public int? MaxRepeat { get; init; }

    /// <summary>
    /// フォーマット既定のエンディアンを上書きする。null ならフォーマット定義の <c>endianness</c> を使う。
    /// struct / フィールド単位の <c>endianness</c> 指定は本オプションより優先される。
    /// </summary>
    public Endianness? Endianness { get; init; }

    /// <summary>
    /// struct / switch の入れ子の深さの上限。超えるとそのフィールドはデコードエラーになる（スタックオーバーフローはプロセスごと落ちるため）。
    /// null なら既定の 64。深くする場合はスタックサイズ（1 段あたり Debug で約 8 KB）に注意。
    /// </summary>
    public int? MaxDepth { get; init; }
}
