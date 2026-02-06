namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedBytes : DecodedNode
{
    public required ReadOnlyMemory<byte> RawBytes { get; init; }
}
