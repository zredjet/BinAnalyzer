namespace BinAnalyzer.Core.Models;

public sealed class ChecksumSpec
{
    public required string Algorithm { get; init; }
    public required IReadOnlyList<string> FieldNames { get; init; }
    public ChecksumRange? Range { get; init; }
    public IReadOnlyList<ChecksumRange>? Ranges { get; init; }
    public bool ExcludeSelf { get; init; }
}
