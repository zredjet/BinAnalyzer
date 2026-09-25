using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Integration.Tests;

/// <summary>デコード結果の木を名前で引くテスト用のヘルパー（子の並びの位置に頼らないため）。</summary>
internal static class DecodedNodeQueries
{
    /// <summary>struct の直下の子を名前で引く。</summary>
    public static DecodedNode Child(this DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);

    /// <summary>配列の要素。</summary>
    public static IReadOnlyList<DecodedNode> Elements(this DecodedNode node) => ((DecodedArray)node).Elements;

    /// <summary>自分と子孫を深さ優先でたどる。</summary>
    public static IEnumerable<DecodedNode> Descendants(this DecodedNode node)
    {
        yield return node;
        var children = node switch
        {
            DecodedStruct s => s.Children,
            DecodedArray a => a.Elements,
            _ => [],
        };
        foreach (var descendant in children.SelectMany(Descendants))
            yield return descendant;
    }

    /// <summary>名前の一致する最初の子孫。</summary>
    public static DecodedNode Find(this DecodedNode node, string name) => node.Descendants().First(n => n.Name == name);

    /// <summary>名前の一致するすべての子孫。</summary>
    public static IEnumerable<DecodedNode> FindAll(this DecodedNode node, string name) => node.Descendants().Where(n => n.Name == name);

    /// <summary>struct の種類の一致するすべての子孫。</summary>
    public static IEnumerable<DecodedStruct> OfStruct(this DecodedNode node, string structType) =>
        node.Descendants().OfType<DecodedStruct>().Where(s => s.StructType == structType);

    public static long Int(this DecodedNode node) => node switch
    {
        DecodedInteger i => i.Value,
        DecodedVirtual { Value: long l } => l,
        DecodedVirtual { Value: int n } => n,
        _ => throw new InvalidOperationException($"{node.Name} は整数ではありません（{node.GetType().Name}）"),
    };

    public static string Str(this DecodedNode node) => node switch
    {
        DecodedString s => s.Value,
        DecodedVirtual v => v.Value.ToString()!,
        _ => throw new InvalidOperationException($"{node.Name} は文字列ではありません（{node.GetType().Name}）"),
    };

    public static string? Label(this DecodedNode node) => node switch
    {
        DecodedInteger i => i.EnumLabel,
        DecodedVirtual v => v.EnumLabel,
        _ => null,
    };

    public static long Bits(this DecodedNode node, string field) => ((DecodedBitfield)node).Fields.First(f => f.Name == field).Value;
}
