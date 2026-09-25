namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedStruct : DecodedNode
{
    public required string StructType { get; init; }
    public required IReadOnlyList<DecodedNode> Children { get; init; }

    /// <summary>独自の変数のスコープを持つ struct（<c>scope: isolated</c>）のデコード結果か。値の昇格はこの struct の中へ入らない（REQ-195）。</summary>
    public bool IsolatedScope { get; init; }
}
