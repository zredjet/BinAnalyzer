namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedArray : DecodedNode
{
    public required IReadOnlyList<DecodedNode> Elements { get; init; }

    /// <summary>diff時にキーベース比較を行う場合の、要素内キーフィールド名リスト。単一キーは要素1のリスト。</summary>
    public IReadOnlyList<string>? DiffKey { get; init; }
}
