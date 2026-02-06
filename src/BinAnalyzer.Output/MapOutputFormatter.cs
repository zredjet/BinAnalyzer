using System.Text;
using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Output;

public sealed class MapOutputFormatter
{
    private const int MaxBarWidth = 64;
    private readonly bool _useColor;

    public MapOutputFormatter(ColorMode mode = ColorMode.Never)
    {
        _useColor = mode switch
        {
            ColorMode.Always => true,
            ColorMode.Never => false,
            ColorMode.Auto => !Console.IsOutputRedirected,
            _ => false,
        };
    }

    private string C(string text, string color)
        => _useColor ? $"{color}{text}{AnsiColors.Reset}" : text;

    public string Format(DecodedStruct root, ReadOnlyMemory<byte> data)
    {
        var fields = new List<FieldRegion>();
        CollectLeafFields(root, "", fields);
        fields.Sort((a, b) => a.Offset.CompareTo(b.Offset));

        var totalSize = data.Length;
        var scale = CalculateScale(totalSize);

        var sb = new StringBuilder();
        sb.AppendLine(C($"BinAnalyzer - Binary Map ({totalSize} bytes)", AnsiColors.Cyan));
        sb.AppendLine(new string('=', 72));

        var alternate = false;
        foreach (var field in fields)
        {
            var barWidth = Math.Max(1, (int)Math.Ceiling(field.Size / scale));
            barWidth = Math.Min(barWidth, MaxBarWidth);

            var bar = alternate ? new string('░', barWidth) : new string('█', barWidth);
            var barColor = alternate ? AnsiColors.Dim : AnsiColors.Yellow;

            sb.Append(C($"0x{field.Offset:X8}", AnsiColors.Dim));
            sb.Append(" ┃");
            sb.Append(C(bar, barColor));
            sb.Append('┃');
            sb.Append(' ');
            sb.Append(C(field.Path, AnsiColors.Cyan));
            sb.Append(C($" ({field.Size} bytes)", AnsiColors.Dim));
            sb.AppendLine();

            alternate = !alternate;
        }

        sb.AppendLine(new string('=', 72));

        var scaleText = scale <= 1.0
            ? "1 char = 1 byte"
            : $"1 char ≈ {scale:F1} bytes";
        sb.AppendLine(C($"Legend: █/░ = fields (alternating), scale: {scaleText}, max {MaxBarWidth} chars", AnsiColors.Dim));

        return sb.ToString();
    }

    private static double CalculateScale(int totalSize)
    {
        if (totalSize <= MaxBarWidth)
            return 1.0;
        return (double)totalSize / MaxBarWidth;
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
