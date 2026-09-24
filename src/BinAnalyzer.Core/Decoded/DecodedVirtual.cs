namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedVirtual : DecodedNode
{
    public required object Value { get; init; }

    /// <summary><c>enum:</c> が付いていて値が整数のとき、対応するラベル（REQ-186）。</summary>
    public string? EnumLabel { get; init; }

    /// <summary><see cref="EnumLabel"/> の説明。</summary>
    public string? EnumDescription { get; init; }
}
