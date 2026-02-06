namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedBitfield : DecodedNode
{
    public required long RawValue { get; init; }
    public required IReadOnlyList<BitfieldValue> Fields { get; init; }
}
