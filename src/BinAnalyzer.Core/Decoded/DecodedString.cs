namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedString : DecodedNode
{
    public required string Value { get; init; }
    public required string Encoding { get; init; }
    public IReadOnlyList<FlagState>? Flags { get; init; }
}
