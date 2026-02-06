using System.Text;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Interfaces;

namespace BinAnalyzer.Output;

public sealed class TreeOutputFormatter : IOutputFormatter
{
    private readonly bool _useColor;

    public TreeOutputFormatter(ColorMode mode = ColorMode.Never)
    {
        _useColor = mode switch
        {
            ColorMode.Always => true,
            ColorMode.Never => false,
            ColorMode.Auto => !Console.IsOutputRedirected,
            _ => false,
        };
    }

    public string Format(DecodedStruct root)
    {
        var sb = new StringBuilder();
        FormatNode(sb, root, "", true, true);
        return sb.ToString();
    }

    private string C(string text, string color)
        => _useColor ? $"{color}{text}{AnsiColors.Reset}" : text;

    private void FormatNode(StringBuilder sb, DecodedNode node, string indent, bool isLast, bool isRoot)
    {
        // パディングフィールドはツリー出力で非表示
        if (node.IsPadding)
            return;

        var connector = isRoot ? "" : isLast ? "└── " : "├── ";
        var prefix = isRoot ? "" : indent + (_useColor ? C(connector, AnsiColors.Dim) : connector);

        switch (node)
        {
            case DecodedStruct structNode:
                FormatStruct(sb, structNode, prefix, indent, isLast, isRoot);
                break;
            case DecodedArray arrayNode:
                FormatArray(sb, arrayNode, prefix, indent, isLast, isRoot);
                break;
            case DecodedInteger intNode:
                FormatInteger(sb, intNode, prefix);
                break;
            case DecodedBytes bytesNode:
                FormatBytes(sb, bytesNode, prefix);
                break;
            case DecodedString stringNode:
                FormatString(sb, stringNode, prefix);
                break;
            case DecodedFloat floatNode:
                FormatFloat(sb, floatNode, prefix);
                break;
            case DecodedFlags flagsNode:
                FormatFlags(sb, flagsNode, prefix);
                break;
            case DecodedBitfield bitfieldNode:
                FormatBitfield(sb, bitfieldNode, prefix, indent, isLast);
                break;
            case DecodedCompressed compressedNode:
                FormatCompressed(sb, compressedNode, prefix, indent, isLast);
                break;
            case DecodedVirtual virtualNode:
                FormatVirtual(sb, virtualNode, prefix);
                break;
            case DecodedError errorNode:
                FormatError(sb, errorNode, prefix);
                break;
        }
    }

    private void FormatStruct(
        StringBuilder sb, DecodedStruct node, string prefix, string indent, bool isLast, bool isRoot)
    {
        sb.Append(prefix);
        if (isRoot)
        {
            sb.Append(node.Name);
        }
        else
        {
            sb.Append(node.Name);
            if (node.StructType != node.Name)
                sb.Append(" → ").Append(node.StructType);
        }
        sb.Append(C($" [0x{node.Offset:X8}] ({node.Size} bytes)", AnsiColors.Dim));
        sb.AppendLine();

        var childIndent = isRoot ? "" : indent + (isLast ? "    " : (_useColor ? C("│   ", AnsiColors.Dim) : "│   "));
        for (var i = 0; i < node.Children.Count; i++)
        {
            FormatNode(sb, node.Children[i], childIndent, i == node.Children.Count - 1, false);
        }
    }

    private void FormatArray(
        StringBuilder sb, DecodedArray node, string prefix, string indent, bool isLast, bool isRoot)
    {
        sb.Append(prefix);
        sb.Append(node.Name);
        sb.Append(C($" [0x{node.Offset:X8}] ({node.Size} bytes) [{node.Elements.Count} items]", AnsiColors.Dim));
        sb.AppendLine();

        var childIndent = isRoot ? "" : indent + (isLast ? "    " : (_useColor ? C("│   ", AnsiColors.Dim) : "│   "));
        for (var i = 0; i < node.Elements.Count; i++)
        {
            var element = node.Elements[i];
            var isLastElement = i == node.Elements.Count - 1;

            if (element is DecodedStruct structElement)
            {
                var elementConnector = isLastElement ? "└── " : "├── ";
                var elementPrefix = childIndent + (_useColor ? C(elementConnector, AnsiColors.Dim) : elementConnector);
                sb.Append(elementPrefix);
                sb.Append($"#{i}");
                if (structElement.StructType != structElement.Name)
                    sb.Append(" → ").Append(structElement.StructType);
                sb.Append(C($" [0x{structElement.Offset:X8}] ({structElement.Size} bytes)", AnsiColors.Dim));
                sb.AppendLine();

                var elementIndent = childIndent + (isLastElement ? "    " : (_useColor ? C("│   ", AnsiColors.Dim) : "│   "));
                for (var j = 0; j < structElement.Children.Count; j++)
                {
                    FormatNode(sb, structElement.Children[j], elementIndent, j == structElement.Children.Count - 1, false);
                }
            }
            else
            {
                FormatNode(sb, element, childIndent, isLastElement, false);
            }
        }
    }

    private void FormatInteger(StringBuilder sb, DecodedInteger node, string prefix)
    {
        sb.Append(prefix);
        sb.Append(node.Name);
        sb.Append(": ");
        sb.Append(C(node.Value.ToString(), AnsiColors.Cyan));
        if (node.Value is >= 16 or <= -16)
            sb.Append(C($" (0x{node.Value:X})", AnsiColors.Dim));
        if (node.EnumLabel is not null)
        {
            sb.Append(' ');
            sb.Append(C($"\"{node.EnumLabel}\"", AnsiColors.Magenta));
            if (node.EnumDescription is not null)
                sb.Append($" - {node.EnumDescription}");
        }
        if (node.ChecksumValid.HasValue)
        {
            if (node.ChecksumValid.Value)
            {
                sb.Append("  ");
                sb.Append(C("✓ (CRC-32)", AnsiColors.Green));
            }
            else
            {
                sb.Append("  ");
                sb.Append(C($"✗ (CRC-32, 期待値: 0x{node.ChecksumExpected:X})", AnsiColors.Red));
            }
        }
        if (node.StringTableValue is not null)
        {
            sb.Append(" → ");
            sb.Append(C($"\"{node.StringTableValue}\"", AnsiColors.Green));
        }
        AppendValidation(sb, node);
        sb.AppendLine();
    }

    private void FormatBytes(StringBuilder sb, DecodedBytes node, string prefix)
    {
        sb.Append(prefix);
        sb.Append(node.Name);
        sb.Append(C($" [0x{node.Offset:X8}] ({node.Size} bytes): ", AnsiColors.Dim));

        var span = node.RawBytes.Span;
        var displayCount = Math.Min(span.Length, 16);
        var hexBuilder = new StringBuilder();
        for (var i = 0; i < displayCount; i++)
        {
            if (i > 0) hexBuilder.Append(' ');
            hexBuilder.Append(span[i].ToString("X2"));
        }
        if (span.Length > 16)
            hexBuilder.Append(" ...");
        sb.Append(C(hexBuilder.ToString(), AnsiColors.Yellow));

        if (node.ValidationPassed.HasValue)
        {
            sb.Append("  ");
            sb.Append(node.ValidationPassed.Value ? C("✓", AnsiColors.Green) : C("✗", AnsiColors.Red));
        }
        AppendValidation(sb, node);

        sb.AppendLine();
    }

    private void FormatString(StringBuilder sb, DecodedString node, string prefix)
    {
        sb.Append(prefix);
        sb.Append(node.Name);
        sb.Append(": ");
        sb.Append(C($"\"{node.Value}\"", AnsiColors.Green));

        if (node.Flags is { Count: > 0 })
        {
            sb.Append("  [");
            var first = true;
            foreach (var flag in node.Flags)
            {
                if (!first) sb.Append(' ');
                first = false;
                sb.Append(flag.Name);
                sb.Append('=');
                sb.Append(flag.Meaning ?? (flag.IsSet ? "set" : "clear"));
            }
            sb.Append(']');
        }
        AppendValidation(sb, node);

        sb.AppendLine();
    }

    private void FormatBitfield(
        StringBuilder sb, DecodedBitfield node, string prefix, string indent, bool isLast)
    {
        sb.Append(prefix);
        sb.Append(node.Name);
        sb.Append(C($" [0x{node.Offset:X8}] ({node.Size} bytes): ", AnsiColors.Dim));
        sb.Append(C($"0x{node.RawValue:X}", AnsiColors.Cyan));
        sb.AppendLine();

        var childIndent = indent + (isLast ? "    " : (_useColor ? C("│   ", AnsiColors.Dim) : "│   "));
        for (var i = 0; i < node.Fields.Count; i++)
        {
            var field = node.Fields[i];
            var isLastField = i == node.Fields.Count - 1;
            var connector = isLastField ? "└── " : "├── ";

            sb.Append(childIndent);
            sb.Append(_useColor ? C(connector, AnsiColors.Dim) : connector);
            sb.Append(field.Name);
            sb.Append(": ");
            sb.Append(C(field.Value.ToString(), AnsiColors.Cyan));

            if (field.BitHigh == field.BitLow)
                sb.Append(C($" (bit {field.BitLow})", AnsiColors.Dim));
            else
                sb.Append(C($" (bits {field.BitHigh}:{field.BitLow})", AnsiColors.Dim));

            if (field.EnumLabel is not null)
            {
                sb.Append(' ');
                sb.Append(C($"\"{field.EnumLabel}\"", AnsiColors.Magenta));
                if (field.EnumDescription is not null)
                    sb.Append($" - {field.EnumDescription}");
            }

            sb.AppendLine();
        }
    }

    private void FormatFloat(StringBuilder sb, DecodedFloat node, string prefix)
    {
        sb.Append(prefix);
        sb.Append(node.Name);
        sb.Append(": ");
        sb.Append(C(node.Value.ToString("G"), AnsiColors.Cyan));
        AppendValidation(sb, node);
        sb.AppendLine();
    }

    private void FormatFlags(StringBuilder sb, DecodedFlags node, string prefix)
    {
        sb.Append(prefix);
        sb.Append(node.Name);
        sb.Append($": ");
        sb.Append(C($"0x{node.RawValue:X}", AnsiColors.Cyan));
        sb.Append("  [");
        var first = true;
        foreach (var flag in node.FlagStates)
        {
            if (!first) sb.Append(' ');
            first = false;
            sb.Append(flag.Name);
            sb.Append('=');
            sb.Append(flag.Meaning ?? (flag.IsSet ? "set" : "clear"));
        }
        sb.Append(']');
        sb.AppendLine();
    }

    private void FormatVirtual(StringBuilder sb, DecodedVirtual node, string prefix)
    {
        sb.Append(prefix);
        sb.Append(node.Name);
        sb.Append(": ");
        sb.Append(C($"= {node.Value}", AnsiColors.Cyan));
        sb.AppendLine();
    }

    private void FormatError(StringBuilder sb, DecodedError node, string prefix)
    {
        sb.Append(prefix);
        sb.Append(C($"✗ {node.Name}", AnsiColors.Red));
        sb.Append(C($" [ERROR at 0x{node.Offset:X8}]: {node.ErrorMessage}", AnsiColors.Red));
        sb.AppendLine();
    }

    private void AppendValidation(StringBuilder sb, DecodedNode node)
    {
        if (node.Validation is { } v)
        {
            sb.Append("  ");
            sb.Append(v.Passed ? C("✓", AnsiColors.Green) : C("✗", AnsiColors.Red));
        }
    }

    private void FormatCompressed(
        StringBuilder sb, DecodedCompressed node, string prefix, string indent, bool isLast)
    {
        sb.Append(prefix);
        sb.Append(node.Name);
        sb.Append(C($" [{node.Algorithm}] ({node.CompressedSize} bytes → {node.DecompressedSize} bytes)", AnsiColors.Dim));
        sb.AppendLine();

        if (node.DecodedContent is not null)
        {
            var childIndent = indent + (isLast ? "    " : (_useColor ? C("│   ", AnsiColors.Dim) : "│   "));
            for (var i = 0; i < node.DecodedContent.Children.Count; i++)
            {
                FormatNode(sb, node.DecodedContent.Children[i], childIndent,
                    i == node.DecodedContent.Children.Count - 1, false);
            }
        }
        else if (node.RawDecompressed is { } raw)
        {
            var childIndent = indent + (isLast ? "    " : (_useColor ? C("│   ", AnsiColors.Dim) : "│   "));
            var connector = "└── ";
            sb.Append(childIndent);
            sb.Append(_useColor ? C(connector, AnsiColors.Dim) : connector);

            var span = raw.Span;
            var displayCount = Math.Min(span.Length, 16);
            var hexBuilder = new StringBuilder();
            for (var i = 0; i < displayCount; i++)
            {
                if (i > 0) hexBuilder.Append(' ');
                hexBuilder.Append(span[i].ToString("X2"));
            }
            if (span.Length > 16)
                hexBuilder.Append(" ...");
            sb.Append(C(hexBuilder.ToString(), AnsiColors.Yellow));
            sb.AppendLine();
        }
    }
}
