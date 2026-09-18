namespace BinAnalyzer.Fuzz.Tests;

/// <summary>
/// ファズテストの規模と乱数シード。既定は CI でも数十秒で終わる小さな決定的セット。
/// <list type="bullet">
/// <item><c>BINANALYZER_FUZZ_ITERATIONS</c> — フォーマット（またはサンプル）1 つあたりの入力数の倍率（既定 1）。ローカルで <c>50</c> 等にして長時間回す</item>
/// <item><c>BINANALYZER_FUZZ_SEED</c> — 乱数シード。未設定なら固定値（再現性のため）。<c>random</c> で毎回変える</item>
/// <item><c>BINANALYZER_FUZZ_TIMEOUT_MS</c> — 1 回のデコードのタイムアウト（既定 5000）</item>
/// </list>
/// 失敗メッセージにはフォーマット・シード・入力（Base64）が入るので、同じシードで再実行すれば再現できる。
/// </summary>
public static class FuzzConfig
{
    public const int DefaultSeed = 20260918;

    public static int Multiplier { get; } = ReadInt("BINANALYZER_FUZZ_ITERATIONS", 1, min: 1);
    public static TimeSpan Timeout { get; } = TimeSpan.FromMilliseconds(ReadInt("BINANALYZER_FUZZ_TIMEOUT_MS", 5000, min: 100));

    public static int Seed { get; } = Environment.GetEnvironmentVariable("BINANALYZER_FUZZ_SEED") switch
    {
        null or "" => DefaultSeed,
        "random" => Environment.TickCount,
        var s when int.TryParse(s, out var v) => v,
        _ => DefaultSeed,
    };

    /// <summary>フォーマット名・テスト名ごとに独立した乱数列を得る（テストの実行順に依存しない）。</summary>
    public static Random RandomFor(string scope)
    {
        var hash = 17;
        foreach (var ch in scope)
            hash = unchecked(hash * 31 + ch);
        return new Random(unchecked(Seed ^ hash));
    }

    private static int ReadInt(string name, int fallback, int min)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return int.TryParse(raw, out var v) && v >= min ? v : fallback;
    }
}
