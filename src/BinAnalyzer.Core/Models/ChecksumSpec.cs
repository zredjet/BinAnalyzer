namespace BinAnalyzer.Core.Models;

public sealed class ChecksumSpec
{
    public required string Algorithm { get; init; }
    public required IReadOnlyList<string> FieldNames { get; init; }
}
