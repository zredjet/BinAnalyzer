namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedArray : DecodedNode
{
    public required IReadOnlyList<DecodedNode> Elements { get; init; }

    /// <summary>diff時にキーベース比較を行う場合の、要素内キーフィールド名リスト。単一キーは要素1のリスト。</summary>
    public IReadOnlyList<string>? DiffKey { get; init; }

    /// <summary>繰り返しがガード条件により打ち切られた場合にtrue。</summary>
    public bool Truncated { get; init; }

    /// <summary>打ち切り理由の説明文字列。Truncated=false時はnull。</summary>
    public string? TruncationReason { get; init; }
}
