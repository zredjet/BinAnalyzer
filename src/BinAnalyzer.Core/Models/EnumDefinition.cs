namespace BinAnalyzer.Core.Models;

public sealed record EnumEntry(long Value, string Label, string? Description = null);

public sealed class EnumDefinition
{
    public required string Name { get; init; }
    public required IReadOnlyList<EnumEntry> Entries { get; init; }

    private Dictionary<long, EnumEntry>? _byValue;

    /// <summary>値に対応するエントリ。同じ値が複数あれば最初のもの。整数フィールドごとに呼ばれるので辞書で引く。</summary>
    public EnumEntry? FindByValue(long value)
    {
        var map = _byValue;
        if (map is null)
        {
            map = new Dictionary<long, EnumEntry>(Entries.Count);
            foreach (var e in Entries)
                map.TryAdd(e.Value, e);
            _byValue = map;
        }
        return map.TryGetValue(value, out var entry) ? entry : null;
    }
}
