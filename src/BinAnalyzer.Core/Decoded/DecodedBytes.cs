namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedBytes : DecodedNode
{
    public required ReadOnlyMemory<byte> RawBytes { get; init; }
    public bool? ChecksumValid { get; init; }
    public string? ChecksumExpectedHex { get; init; }
    public string? ChecksumAlgorithm { get; init; }
}
