namespace BinAnalyzer.Core.Models;

public sealed class StructDefinition
{
    public required string Name { get; init; }
    public required IReadOnlyList<FieldDefinition> Fields { get; init; }

    /// <summary>この構造体のインスタンス開始位置を指定バイト境界にアラインする（繰り返し時）。</summary>
    public int? Align { get; init; }
}
