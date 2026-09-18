using System.Diagnostics;

namespace BinAnalyzer.Gui.State;

/// <summary>
/// 性能計測用のログ（REQ-177）。環境変数 <c>BINANALYZER_GUI_TIMING=1</c> のときだけ標準エラーに
/// 「開く」の各段階・最初の描画・選択の反映までの所要時間を出す。通常は何もしない。
/// </summary>
public static class GuiTiming
{
    public static bool Enabled { get; } = Environment.GetEnvironmentVariable("BINANALYZER_GUI_TIMING") == "1";

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly double ProcessStartOffsetMs = ProcessStartOffset();
    private static long _selectStartedAt = -1;

    /// <summary>プロセス起動からの経過時間（ミリ秒）。</summary>
    public static double Now => ProcessStartOffsetMs + Clock.Elapsed.TotalMilliseconds;

    /// <summary>
    /// 計測専用: <c>BINANALYZER_GUI_TIMING_SELECT=&lt;nodeId&gt;</c> で、最初の描画から 3 秒後にそのノードを選択して反映時間を測る。
    /// </summary>
    public static int? AutoSelectId { get; } =
        int.TryParse(Environment.GetEnvironmentVariable("BINANALYZER_GUI_TIMING_SELECT"), out var id) ? id : null;

    private static double ProcessStartOffset()
    {
        try
        {
            return (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds;
        }
        catch (Exception)
        {
            return 0; // WASM 等で取れない環境では GuiTiming 初期化時点を 0 とする
        }
    }

    public static void Log(string message)
    {
        if (Enabled)
            Console.Error.WriteLine($"[gui-timing] {Now,10:F1} ms  {message}");
    }

    /// <summary>選択操作の開始を記録する。次の <see cref="LogSelectRendered"/> で反映までの時間を出す。</summary>
    public static void MarkSelect()
    {
        if (Enabled)
            Interlocked.Exchange(ref _selectStartedAt, Clock.ElapsedTicks);
    }

    /// <summary>選択後の描画完了。<see cref="MarkSelect"/> が無ければ何もしない。</summary>
    public static void LogSelectRendered(string where)
    {
        if (!Enabled) return;
        var started = Interlocked.Exchange(ref _selectStartedAt, -1);
        if (started < 0) return;
        var ms = (Clock.ElapsedTicks - started) * 1000.0 / Stopwatch.Frequency;
        Console.Error.WriteLine($"[gui-timing] {Now,10:F1} ms  select → {where} rendered: {ms:F1} ms");
    }
}
