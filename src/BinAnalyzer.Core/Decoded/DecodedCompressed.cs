namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedCompressed : DecodedNode
{
    public required int CompressedSize { get; init; }
    public required int DecompressedSize { get; init; }
    public required string Algorithm { get; init; }
    public DecodedStruct? DecodedContent { get; init; }
    public ReadOnlyMemory<byte>? RawDecompressed { get; init; }
}
