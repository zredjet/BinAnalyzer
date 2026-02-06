namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedFlags : DecodedNode
{
    public required long RawValue { get; init; }
    public required IReadOnlyList<FlagState> FlagStates { get; init; }
}
