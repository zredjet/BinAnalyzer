namespace BinAnalyzer.Core.Expressions;

/// <summary>
/// 式の組み込み関数の名前（評価は Engine の <c>ExpressionEvaluator</c>）。検証器が未知の関数（VAL125）の判定に使う。
/// 関数を足すときは評価器と docs/dsl-reference.md の「組み込み関数」も更新する（テストで一致を確かめている。REQ-189）。
/// </summary>
public static class BuiltinFunctions
{
    /// <summary>組み込み関数の名前（dsl-reference.md の記載順）。</summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        "until_marker",
        "parse_int",
        "len",
        "count",
        "min",
        "max",
        "sum",
        "substr",
        "concat",
        "contains",
        "hex",
        "upper",
        "lower",
        "trim",
        "popcount",
        "abs",
        "log2",
    ];

    private static readonly HashSet<string> NameSet = new(Names, StringComparer.Ordinal);

    public static bool IsBuiltin(string name) => NameSet.Contains(name);
}
