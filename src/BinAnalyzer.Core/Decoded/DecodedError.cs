namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedError : DecodedNode
{
    public required string ErrorMessage { get; init; }
    public string? FieldType { get; init; }
}
