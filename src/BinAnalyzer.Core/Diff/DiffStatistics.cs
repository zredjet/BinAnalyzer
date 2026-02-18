namespace BinAnalyzer.Core.Diff;

public sealed record DiffStatistics(
    int ChangedCount,
    int AddedCount,
    int RemovedCount,
    int UnchangedCount)
{
    public int TotalDiffCount => ChangedCount + AddedCount + RemovedCount;
    public int TotalFieldCount => ChangedCount + AddedCount + RemovedCount + UnchangedCount;
    public double MatchRate => TotalFieldCount > 0
        ? (double)UnchangedCount / TotalFieldCount
        : 1.0;
}
