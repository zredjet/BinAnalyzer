using System.Text;
using BinAnalyzer.Core.Diff;

namespace BinAnalyzer.Output;

public sealed class DiffOutputFormatter
{
    private readonly bool _useColor;

    public DiffOutputFormatter(ColorMode mode = ColorMode.Never)
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

    public string Format(DiffResult result)
    {
        if (!result.HasDifferences)
            return C("差分なし", AnsiColors.Green) + "\n";

        var sb = new StringBuilder();
        sb.AppendLine(C($"差分: {result.Entries.Count} 件", AnsiColors.Cyan));
        sb.AppendLine();

        foreach (var entry in result.Entries)
        {
            switch (entry.Kind)
            {
                case DiffKind.Changed:
                    sb.AppendLine(C($"  ~ {entry.FieldPath}: {entry.OldValue} → {entry.NewValue}", AnsiColors.Yellow));
                    break;
                case DiffKind.Added:
                    sb.AppendLine(C($"  + {entry.FieldPath}: {entry.NewValue}", AnsiColors.Green));
                    break;
                case DiffKind.Removed:
                    sb.AppendLine(C($"  - {entry.FieldPath}: {entry.OldValue}", AnsiColors.Red));
                    break;
            }
        }

        return sb.ToString();
    }
}
