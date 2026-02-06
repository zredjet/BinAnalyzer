namespace BinAnalyzer.Core.Decoded;

public sealed record FlagState(
    string Name,
    bool IsSet,
    int BitPosition,
    string? Meaning);
