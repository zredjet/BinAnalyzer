namespace BinAnalyzer.Core.Diff;

public sealed record DiffEntry(
    DiffKind Kind,
    string FieldPath,
    string? OldValue,
    string? NewValue);
