namespace BinAnalyzer.Core.Diff;

public sealed class BatchDiffResult
{
    public required IReadOnlyList<BatchDiffFileEntry> FileEntries { get; init; }
    public required IReadOnlyList<string> LeftOnlyFiles { get; init; }
    public required IReadOnlyList<string> RightOnlyFiles { get; init; }

    public bool HasDifferences =>
        FileEntries.Any(e => e.HasDifferences || e.HasError) ||
        LeftOnlyFiles.Count > 0 ||
        RightOnlyFiles.Count > 0;
}

public sealed class BatchDiffFileEntry
{
    public required string FileName { get; init; }
    public DiffStatistics? Statistics { get; init; }
    public bool HasDifferences { get; init; }
    public bool HasError { get; init; }
    public string? ErrorMessage { get; init; }
}
