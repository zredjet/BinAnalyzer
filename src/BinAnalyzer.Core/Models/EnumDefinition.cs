namespace BinAnalyzer.Core.Models;

public sealed record EnumEntry(long Value, string Label, string? Description = null);

public sealed class EnumDefinition
{
    public required string Name { get; init; }
    public required IReadOnlyList<EnumEntry> Entries { get; init; }

    public EnumEntry? FindByValue(long value) =>
        Entries.FirstOrDefault(e => e.Value == value);
}
