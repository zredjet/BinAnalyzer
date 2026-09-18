namespace BinAnalyzer.Engine;

/// <summary>
/// 小さな整数のボックス化を使い回す。デコード中は整数フィールドの値・配列インデックス・struct のメンバー値を
/// <c>object</c> として変数に束縛するため、ノード 1 個あたり十数回のボックス化（各 24 B）が発生していた（REQ-180）。
/// 長さ・個数・ポート番号・フラグの多くはこの範囲に収まる。
/// </summary>
internal static class BoxCache
{
    private const long Min = -1024;
    private const long Max = 16383;
    private static readonly object?[] Cache = new object?[Max - Min + 1];

    public static object Box(long value)
    {
        if (value < Min || value > Max)
            return value;
        var i = (int)(value - Min);
        return Cache[i] ??= value;
    }
}
