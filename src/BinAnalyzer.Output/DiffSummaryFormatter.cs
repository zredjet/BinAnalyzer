using System.Text;
using BinAnalyzer.Core.Diff;

namespace BinAnalyzer.Output;

public sealed class DiffSummaryFormatter
{
    private readonly bool _useColor;

    public DiffSummaryFormatter(ColorMode mode = ColorMode.Never)
    {
        _useColor = mode switch
        {
            ColorMode.Always => true,
            ColorMode.Never => false,
            ColorMode.Auto => !Console.IsOutputRedirected,
            _ => false,
        };
    }

    private string C(string text, string color)
        => _useColor ? $"{color}{text}{AnsiColors.Reset}" : text;

    public string Format(DiffStatistics statistics)
    {
        var sb = new StringBuilder();
        sb.AppendLine(C("--- 統計 ---", AnsiColors.Cyan));

        if (statistics.TotalDiffCount == 0)
        {
            sb.AppendLine(C("  差分なし", AnsiColors.Green));
        }
        else
        {
            sb.AppendLine($"  変更: {statistics.ChangedCount} 件");
            sb.AppendLine($"  追加: {statistics.AddedCount} 件");
            sb.AppendLine($"  削除: {statistics.RemovedCount} 件");
            sb.AppendLine($"  合計差分: {statistics.TotalDiffCount} 件");
        }

        var matchPercent = statistics.MatchRate * 100;
        sb.AppendLine($"  一致: {statistics.UnchangedCount} / {statistics.TotalFieldCount} フィールド ({matchPercent:F1}%)");

        return sb.ToString();
    }
}
