namespace BinAnalyzer.Core.Decoded;

public sealed record BitfieldValue(
    string Name,
    int BitHigh,
    int BitLow,
    long Value,
    string? EnumLabel,
    string? EnumDescription)
{
    /// <summary>値の 10 進の表記（エントリは符号なし。64 ビットの幅の上位ビットも正の値。REQ-201）。</summary>
    public string ValueText => Models.IntegerText.FormatBits(Value);
}
