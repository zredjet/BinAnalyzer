namespace BinAnalyzer.Core.Decoded;

public sealed record BitfieldValue(
    string Name,
    int BitHigh,
    int BitLow,
    long Value,
    string? EnumLabel,
    string? EnumDescription);
