namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedArray : DecodedNode
{
    public required IReadOnlyList<DecodedNode> Elements { get; init; }
}
