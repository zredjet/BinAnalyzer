namespace BinAnalyzer.Core.Diff;

public sealed class DiffResult
{
    public required IReadOnlyList<DiffEntry> Entries { get; init; }
    public bool HasDifferences => Entries.Count > 0;
}
