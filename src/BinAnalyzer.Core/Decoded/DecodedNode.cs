namespace BinAnalyzer.Core.Decoded;

public abstract class DecodedNode
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required long Offset { get; init; }
    public required long Size { get; init; }
    public bool? ValidationPassed { get; init; }
}
