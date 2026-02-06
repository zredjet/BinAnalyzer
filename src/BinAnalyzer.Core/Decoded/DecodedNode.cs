namespace BinAnalyzer.Core.Decoded;

public abstract class DecodedNode
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required long Offset { get; init; }
    public required long Size { get; init; }
    public bool? ValidationPassed { get; init; }

    /// <summary>パディングフィールドの場合true。出力時にデフォルト非表示。</summary>
    public bool IsPadding { get; init; }

    /// <summary>カスタムバリデーション式の結果。</summary>
    public ValidationInfo? Validation { get; init; }
}
