namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedInteger : DecodedNode
{
    public required long Value { get; init; }
    public string? EnumLabel { get; init; }
    public string? EnumDescription { get; init; }
    public bool? ChecksumValid { get; init; }
    public long? ChecksumExpected { get; init; }
}
