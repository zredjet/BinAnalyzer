using System.Text;
using BinAnalyzer.Core.Diff;

namespace BinAnalyzer.Output;

public sealed class BatchDiffSummaryFormatter
{
    private readonly bool _useColor;

    public BatchDiffSummaryFormatter(ColorMode mode = ColorMode.Never)
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

    public string Format(BatchDiffResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine(C("--- バッチdiffレポート ---", AnsiColors.Cyan));
        sb.AppendLine();

        if (result.FileEntries.Count > 0)
        {
            sb.AppendLine("ファイル比較結果:");
            sb.AppendLine();

            // Header
            sb.AppendLine($"  {"ファイル名",-36} {"結果",-12} {"変更",4}  {"追加",4}  {"削除",4}");
            sb.AppendLine($"  {new string('-', 70)}");

            foreach (var entry in result.FileEntries)
            {
                if (entry.HasError)
                {
                    var status = C("エラー", AnsiColors.Red);
                    sb.AppendLine($"  {entry.FileName,-36} {status,-12} {entry.ErrorMessage}");
                }
                else if (entry.HasDifferences)
                {
                    var stats = entry.Statistics!;
                    var status = C("差分あり", AnsiColors.Yellow);
                    sb.AppendLine($"  {entry.FileName,-36} {status,-12} {stats.ChangedCount,4}  {stats.AddedCount,4}  {stats.RemovedCount,4}");
                }
                else
                {
                    var status = C("同一", AnsiColors.Green);
                    sb.AppendLine($"  {entry.FileName,-36} {status}");
                }
            }

            sb.AppendLine();
        }

        if (result.LeftOnlyFiles.Count > 0)
        {
            sb.AppendLine($"左ディレクトリのみ ({result.LeftOnlyFiles.Count} 件):");
            foreach (var f in result.LeftOnlyFiles)
                sb.AppendLine($"  - {f}");
            sb.AppendLine();
        }

        if (result.RightOnlyFiles.Count > 0)
        {
            sb.AppendLine($"右ディレクトリのみ ({result.RightOnlyFiles.Count} 件):");
            foreach (var f in result.RightOnlyFiles)
                sb.AppendLine($"  + {f}");
            sb.AppendLine();
        }

        // Summary line
        var identicalCount = result.FileEntries.Count(e => !e.HasDifferences && !e.HasError);
        var diffCount = result.FileEntries.Count(e => e.HasDifferences && !e.HasError);
        var errorCount = result.FileEntries.Count(e => e.HasError);

        sb.Append($"合計: {result.FileEntries.Count} ファイル比較, {identicalCount} 同一, {diffCount} 差分あり");
        if (errorCount > 0)
            sb.Append($", {errorCount} エラー");
        sb.AppendLine();

        return sb.ToString();
    }
}
