namespace BinAnalyzer.Core.Decoded;

public sealed class DecodeResult
{
    public required DecodedStruct Root { get; init; }
    public required IReadOnlyList<DecodeError> Errors { get; init; }
}
