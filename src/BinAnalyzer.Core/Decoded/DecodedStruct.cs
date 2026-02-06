namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedStruct : DecodedNode
{
    public required string StructType { get; init; }
    public required IReadOnlyList<DecodedNode> Children { get; init; }
}
