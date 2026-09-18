using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Fuzz.Tests;

/// <summary>デコード結果ツリーが満たすべき不変条件。破れていれば説明を返す。</summary>
public static class TreeInvariants
{
    public static List<string> Check(DecodedStruct root, long dataLength)
    {
        var problems = new List<string>();
        var count = 0;
        Walk(root, dataLength, "", problems, ref count);
        return problems;
    }

    private static void Walk(DecodedNode node, long spaceLength, string path, List<string> problems, ref int count)
    {
        count++;
        if (count > 5_000_000)
        {
            problems.Add("more than 5,000,000 nodes");
            return;
        }
        if (node.Size < 0)
            problems.Add($"{path}: negative size {node.Size}");
        if (node.Offset < 0)
            problems.Add($"{path}: negative offset {node.Offset}");
        // DecodedError はサイズ 0 で「失敗した位置」を指すので終端と等しくてよい
        if (node.Offset > spaceLength)
            problems.Add($"{path}: offset {node.Offset} beyond data length {spaceLength}");
        if (node.Size > 0 && node.Offset + node.Size > spaceLength)
            problems.Add($"{path}: range [{node.Offset}, {node.Offset + node.Size}) beyond data length {spaceLength}");
        if (string.IsNullOrEmpty(node.Name))
            problems.Add($"{path}: empty name");

        switch (node)
        {
            case DecodedStruct s:
                for (var i = 0; i < s.Children.Count; i++)
                    Walk(s.Children[i], spaceLength, $"{path}/{s.Children[i].Name}", problems, ref count);
                break;
            case DecodedArray a:
                for (var i = 0; i < a.Elements.Count; i++)
                    Walk(a.Elements[i], spaceLength, $"{path}[{i}]", problems, ref count);
                break;
            case DecodedCompressed { DecodedContent: { } content } c:
                // 展開内容は「ストリーム空間」: 展開後サイズを上限にする
                Walk(content, c.RawDecompressed?.Length ?? c.DecompressedSize, $"{path}/{content.Name}", problems, ref count);
                break;
            case DecodedBytes b when b.RawBytes.Length != b.Size && b.Size > 0:
                problems.Add($"{path}: RawBytes length {b.RawBytes.Length} != Size {b.Size}");
                break;
        }
    }
}
