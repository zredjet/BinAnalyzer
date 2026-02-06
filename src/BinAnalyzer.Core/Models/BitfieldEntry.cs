namespace BinAnalyzer.Core.Models;

/// <summary>
/// ビットフィールド内の個別エントリ定義。
/// </summary>
public sealed class BitfieldEntry
{
    public required string Name { get; init; }

    /// <summary>上位ビット位置。単一ビットの場合は BitLow と同値。</summary>
    public required int BitHigh { get; init; }

    /// <summary>下位ビット位置。</summary>
    public required int BitLow { get; init; }

    /// <summary>オプションのenum参照。</summary>
    public string? EnumRef { get; init; }

    public string? Description { get; init; }
}
