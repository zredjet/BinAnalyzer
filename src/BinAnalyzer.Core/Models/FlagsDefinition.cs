namespace BinAnalyzer.Core.Models;

public sealed record FlagFieldDefinition(
    string Name,
    int BitPosition,
    int BitSize,
    string? SetMeaning = null,
    string? ClearMeaning = null);

public sealed class FlagsDefinition
{
    public required string Name { get; init; }
    public required int BitSize { get; init; }
    public required IReadOnlyList<FlagFieldDefinition> Fields { get; init; }
}
