using System.Globalization;

namespace BinAnalyzer.Gui.Desktop;

/// <summary>
/// テスト専用の自動終了オプション。環境変数 <c>BINANALYZER_GUI_AUTOCLOSE</c> で指定する。
/// <list type="bullet">
/// <item><c>1</c> / <c>true</c> — 既定の待ち時間（3 秒）で閉じる</item>
/// <item>正の整数 — ミリ秒</item>
/// <item>未設定 / 空 / <c>0</c> / <c>false</c> — 自動終了しない</item>
/// </list>
/// CI の GUI 起動スモークで「窓が開いて閉じられ、終了コード 0 で戻る」ことを確認するために使う。
/// </summary>
public static class GuiAutoClose
{
    public const string EnvironmentVariable = "BINANALYZER_GUI_AUTOCLOSE";
    public static readonly TimeSpan Default = TimeSpan.FromSeconds(3);

    /// <summary>環境変数の現在値を解釈する。</summary>
    public static TimeSpan? FromEnvironment()
        => Parse(Environment.GetEnvironmentVariable(EnvironmentVariable));

    public static TimeSpan? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var v = value.Trim();
        if (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase))
            return Default;
        if (v.Equals("false", StringComparison.OrdinalIgnoreCase) || v.Equals("no", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n <= 0)
            return null;

        // "1" は「有効化」の慣用表記。1 ms で閉じたい人はいないので既定値に丸める
        return n == 1 ? Default : TimeSpan.FromMilliseconds(n);
    }
}
