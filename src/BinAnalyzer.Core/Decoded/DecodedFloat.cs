namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedFloat : DecodedNode
{
    public required double Value { get; init; }
    public required bool IsSinglePrecision { get; init; }
}
