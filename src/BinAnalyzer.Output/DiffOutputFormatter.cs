using System.Text;
using BinAnalyzer.Core.Diff;

namespace BinAnalyzer.Output;

public sealed class DiffOutputFormatter
{
    public string Format(DiffResult result)
    {
        if (!result.HasDifferences)
            return "差分なし\n";

        var sb = new StringBuilder();
        sb.AppendLine($"差分: {result.Entries.Count} 件");
        sb.AppendLine();

        foreach (var entry in result.Entries)
        {
            switch (entry.Kind)
            {
                case DiffKind.Changed:
                    sb.AppendLine($"  ~ {entry.FieldPath}: {entry.OldValue} → {entry.NewValue}");
                    break;
                case DiffKind.Added:
                    sb.AppendLine($"  + {entry.FieldPath}: {entry.NewValue}");
                    break;
                case DiffKind.Removed:
                    sb.AppendLine($"  - {entry.FieldPath}: {entry.OldValue}");
                    break;
            }
        }

        return sb.ToString();
    }
}
