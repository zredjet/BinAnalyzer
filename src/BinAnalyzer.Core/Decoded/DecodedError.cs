namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedError : DecodedNode
{
    public required string ErrorMessage { get; init; }
    public string? FieldType { get; init; }

    /// <summary>エラー回復時にスキップしたバイト数。</summary>
    public int SkippedBytes { get; init; }
}
