using System.Text;
using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Output;

public sealed class HexDumpOutputFormatter
{
    public string Format(DecodedStruct root, ReadOnlyMemory<byte> data)
    {
        var fields = new List<FieldRegion>();
        CollectLeafFields(root, "", fields);
        fields.Sort((a, b) => a.Offset.CompareTo(b.Offset));

        var sb = new StringBuilder();

        // ヘッダー行
        sb.AppendLine("Offset    00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F  ASCII            Field");
        sb.AppendLine("────────  ─────────────────────────────────────────────────  ───────────────  ─────────────────────");

        var span = data.Span;
        var fieldIndex = 0;

        while (fieldIndex < fields.Count)
        {
            var field = fields[fieldIndex];
            var fieldStart = (int)field.Offset;
            var fieldEnd = fieldStart + (int)field.Size;
            fieldEnd = Math.Min(fieldEnd, span.Length);

            var pos = fieldStart;
            var isFirstLine = true;

            while (pos < fieldEnd)
            {
                var lineEnd = Math.Min(pos + 16, fieldEnd);
                // 16バイト境界にも揃える
                var alignedEnd = ((pos / 16) + 1) * 16;
                lineEnd = Math.Min(lineEnd, alignedEnd);
                var count = lineEnd - pos;

                FormatLine(sb, span, pos, count, isFirstLine ? field.Path : "");
                isFirstLine = false;
                pos = lineEnd;
            }

            fieldIndex++;
        }

        return sb.ToString();
    }

    private static void FormatLine(StringBuilder sb, ReadOnlySpan<byte> data, int offset, int count, string fieldPath)
    {
        // オフセット
        sb.Append(offset.ToString("X8"));
        sb.Append("  ");

        // 16バイト行内の開始位置
        var lineStart = offset % 16;

        // Hex部分（16カラム分のスペースを確保）
        for (var col = 0; col < 16; col++)
        {
            if (col == 8) sb.Append(' ');

            if (col >= lineStart && col < lineStart + count)
            {
                sb.Append(data[offset + col - lineStart].ToString("X2"));
                sb.Append(' ');
            }
            else
            {
                sb.Append("   ");
            }
        }

        sb.Append(' ');

        // ASCII部分（16カラム分）
        for (var col = 0; col < 16; col++)
        {
            if (col >= lineStart && col < lineStart + count)
            {
                var b = data[offset + col - lineStart];
                sb.Append(b is >= 0x20 and <= 0x7E ? (char)b : '.');
            }
            else
            {
                sb.Append(' ');
            }
        }

        // フィールド名
        if (fieldPath.Length > 0)
        {
            sb.Append("  ");
            sb.Append(fieldPath);
        }

        sb.AppendLine();
    }

    private static void CollectLeafFields(DecodedNode node, string parentPath, List<FieldRegion> fields)
    {
        switch (node)
        {
            case DecodedStruct structNode:
            {
                var path = parentPath.Length == 0 ? "" : parentPath;
                foreach (var child in structNode.Children)
                {
                    var childPath = path.Length == 0 ? child.Name : $"{path}.{child.Name}";
                    CollectLeafFields(child, childPath, fields);
                }
                break;
            }
            case DecodedArray arrayNode:
            {
                var basePath = parentPath;
                for (var i = 0; i < arrayNode.Elements.Count; i++)
                {
                    var element = arrayNode.Elements[i];
                    var elementPath = $"{basePath}[{i}]";
                    CollectLeafFields(element, elementPath, fields);
                }
                break;
            }
            default:
                if (node.Size > 0)
                    fields.Add(new FieldRegion(node.Offset, node.Size, parentPath));
                break;
        }
    }

    private readonly record struct FieldRegion(long Offset, long Size, string Path);
}
