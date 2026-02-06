namespace BinAnalyzer.Core.Models;

public sealed class StructDefinition
{
    public required string Name { get; init; }
    public required IReadOnlyList<FieldDefinition> Fields { get; init; }
}
