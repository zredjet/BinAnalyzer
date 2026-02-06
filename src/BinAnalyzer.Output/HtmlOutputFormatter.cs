using System.Net;
using System.Text;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Interfaces;

namespace BinAnalyzer.Output;

public sealed class HtmlOutputFormatter : IOutputFormatter
{
    public string Format(DecodedStruct root)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"ja\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.Append("<title>BinAnalyzer - ").Append(E(root.Name)).AppendLine("</title>");
        WriteStyle(sb);
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        WriteToolbar(sb);
        sb.AppendLine("<div class=\"tree\" id=\"root\">");
        WriteNode(sb, root, 0, true);
        sb.AppendLine("</div>");
        WriteScript(sb);
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string E(string text) => WebUtility.HtmlEncode(text);

    private static void WriteToolbar(StringBuilder sb)
    {
        sb.AppendLine("<div class=\"toolbar\">");
        sb.AppendLine("  <button onclick=\"expandAll()\">全展開</button>");
        sb.AppendLine("  <button onclick=\"collapseAll()\">全折りたたみ</button>");
        sb.AppendLine("</div>");
    }

    private static void WriteNode(StringBuilder sb, DecodedNode node, int depth, bool isRoot)
    {
        switch (node)
        {
            case DecodedStruct structNode:
                WriteStructNode(sb, structNode, depth, isRoot);
                break;
            case DecodedArray arrayNode:
                WriteArrayNode(sb, arrayNode, depth);
                break;
            case DecodedInteger intNode:
                WriteIntegerNode(sb, intNode);
                break;
            case DecodedBytes bytesNode:
                WriteBytesNode(sb, bytesNode);
                break;
            case DecodedString stringNode:
                WriteStringNode(sb, stringNode);
                break;
            case DecodedFloat floatNode:
                WriteFloatNode(sb, floatNode);
                break;
            case DecodedFlags flagsNode:
                WriteFlagsNode(sb, flagsNode);
                break;
            case DecodedBitfield bitfieldNode:
                WriteBitfieldNode(sb, bitfieldNode, depth);
                break;
            case DecodedCompressed compressedNode:
                WriteCompressedNode(sb, compressedNode, depth);
                break;
        }
    }

    private static void WriteStructNode(StringBuilder sb, DecodedStruct node, int depth, bool isRoot)
    {
        var collapsed = depth > 1 ? " collapsed" : "";
        sb.Append("<div class=\"node struct collapsible").Append(collapsed).AppendLine("\">");
        sb.Append("  <div class=\"header\" onclick=\"toggle(this)\">");
        sb.Append("<span class=\"toggle\">").Append(depth > 1 ? "▶" : "▼").Append("</span>");
        sb.Append("<span class=\"name\">").Append(E(node.Name)).Append("</span>");
        if (!isRoot && node.StructType != node.Name)
            sb.Append(" <span class=\"meta\">→ ").Append(E(node.StructType)).Append("</span>");
        sb.Append(" <span class=\"meta\">[0x").Append(node.Offset.ToString("X8")).Append("] (")
            .Append(node.Size).Append(" bytes)</span>");
        sb.AppendLine("</div>");
        sb.Append("  <div class=\"children\"").Append(depth > 1 ? " style=\"display:none\"" : "").AppendLine(">");
        foreach (var child in node.Children)
            WriteNode(sb, child, depth + 1, false);
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");
    }

    private static void WriteArrayNode(StringBuilder sb, DecodedArray node, int depth)
    {
        var collapsed = depth > 1 ? " collapsed" : "";
        sb.Append("<div class=\"node array collapsible").Append(collapsed).AppendLine("\">");
        sb.Append("  <div class=\"header\" onclick=\"toggle(this)\">");
        sb.Append("<span class=\"toggle\">").Append(depth > 1 ? "▶" : "▼").Append("</span>");
        sb.Append("<span class=\"name\">").Append(E(node.Name)).Append("</span>");
        sb.Append(" <span class=\"meta\">[0x").Append(node.Offset.ToString("X8")).Append("] (")
            .Append(node.Size).Append(" bytes) [").Append(node.Elements.Count).Append(" items]</span>");
        sb.AppendLine("</div>");
        sb.Append("  <div class=\"children\"").Append(depth > 1 ? " style=\"display:none\"" : "").AppendLine(">");
        for (var i = 0; i < node.Elements.Count; i++)
        {
            var element = node.Elements[i];
            if (element is DecodedStruct structElement)
            {
                sb.AppendLine("<div class=\"node struct collapsible collapsed\">");
                sb.Append("  <div class=\"header\" onclick=\"toggle(this)\">");
                sb.Append("<span class=\"toggle\">▶</span>");
                sb.Append("<span class=\"name\">#").Append(i).Append("</span>");
                if (structElement.StructType != structElement.Name)
                    sb.Append(" <span class=\"meta\">→ ").Append(E(structElement.StructType)).Append("</span>");
                sb.Append(" <span class=\"meta\">[0x").Append(structElement.Offset.ToString("X8")).Append("] (")
                    .Append(structElement.Size).Append(" bytes)</span>");
                sb.AppendLine("</div>");
                sb.AppendLine("  <div class=\"children\" style=\"display:none\">");
                foreach (var child in structElement.Children)
                    WriteNode(sb, child, depth + 2, false);
                sb.AppendLine("  </div>");
                sb.AppendLine("</div>");
            }
            else
            {
                WriteNode(sb, element, depth + 1, false);
            }
        }
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");
    }

    private static void WriteIntegerNode(StringBuilder sb, DecodedInteger node)
    {
        sb.AppendLine("<div class=\"node integer\">");
        sb.Append("  <span class=\"name\">").Append(E(node.Name)).Append("</span>: ");
        sb.Append("<span class=\"value int\">").Append(node.Value).Append("</span>");
        if (node.Value is >= 16 or <= -16)
            sb.Append(" <span class=\"meta\">(0x").Append(node.Value.ToString("X")).Append(")</span>");
        if (node.EnumLabel is not null)
        {
            sb.Append(" <span class=\"value enum\">\"").Append(E(node.EnumLabel)).Append("\"</span>");
            if (node.EnumDescription is not null)
                sb.Append(" <span class=\"desc\">- ").Append(E(node.EnumDescription)).Append("</span>");
        }
        if (node.ChecksumValid.HasValue)
        {
            if (node.ChecksumValid.Value)
                sb.Append(" <span class=\"valid\">✓</span>");
            else
                sb.Append(" <span class=\"invalid\">✗ (expected: 0x").Append(node.ChecksumExpected?.ToString("X") ?? "?").Append(")</span>");
        }
        sb.AppendLine();
        sb.AppendLine("</div>");
    }

    private static void WriteBytesNode(StringBuilder sb, DecodedBytes node)
    {
        sb.AppendLine("<div class=\"node bytes\">");
        sb.Append("  <span class=\"name\">").Append(E(node.Name)).Append("</span>");
        sb.Append(" <span class=\"meta\">[0x").Append(node.Offset.ToString("X8")).Append("] (")
            .Append(node.Size).Append(" bytes)</span>: ");

        var span = node.RawBytes.Span;
        var displayCount = Math.Min(span.Length, 16);
        sb.Append("<span class=\"value hex\">");
        for (var i = 0; i < displayCount; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(span[i].ToString("X2"));
        }
        if (span.Length > 16) sb.Append(" ...");
        sb.Append("</span>");

        if (node.ValidationPassed.HasValue)
        {
            sb.Append(node.ValidationPassed.Value
                ? " <span class=\"valid\">✓</span>"
                : " <span class=\"invalid\">✗</span>");
        }
        sb.AppendLine();
        sb.AppendLine("</div>");
    }

    private static void WriteStringNode(StringBuilder sb, DecodedString node)
    {
        sb.AppendLine("<div class=\"node string\">");
        sb.Append("  <span class=\"name\">").Append(E(node.Name)).Append("</span>: ");
        sb.Append("<span class=\"value str\">\"").Append(E(node.Value)).Append("\"</span>");
        if (node.Flags is { Count: > 0 })
        {
            sb.Append("  <span class=\"meta\">[");
            var first = true;
            foreach (var flag in node.Flags)
            {
                if (!first) sb.Append(' ');
                first = false;
                sb.Append(E(flag.Name)).Append('=').Append(E(flag.Meaning ?? (flag.IsSet ? "set" : "clear")));
            }
            sb.Append("]</span>");
        }
        sb.AppendLine();
        sb.AppendLine("</div>");
    }

    private static void WriteFloatNode(StringBuilder sb, DecodedFloat node)
    {
        sb.AppendLine("<div class=\"node float\">");
        sb.Append("  <span class=\"name\">").Append(E(node.Name)).Append("</span>: ");
        sb.Append("<span class=\"value int\">").Append(node.Value.ToString("G")).Append("</span>");
        sb.AppendLine();
        sb.AppendLine("</div>");
    }

    private static void WriteFlagsNode(StringBuilder sb, DecodedFlags node)
    {
        sb.AppendLine("<div class=\"node flags\">");
        sb.Append("  <span class=\"name\">").Append(E(node.Name)).Append("</span>: ");
        sb.Append("<span class=\"value int\">0x").Append(node.RawValue.ToString("X")).Append("</span>");
        sb.Append("  <span class=\"meta\">[");
        var first = true;
        foreach (var flag in node.FlagStates)
        {
            if (!first) sb.Append(' ');
            first = false;
            sb.Append(E(flag.Name)).Append('=').Append(E(flag.Meaning ?? (flag.IsSet ? "set" : "clear")));
        }
        sb.Append("]</span>");
        sb.AppendLine();
        sb.AppendLine("</div>");
    }

    private static void WriteBitfieldNode(StringBuilder sb, DecodedBitfield node, int depth)
    {
        sb.AppendLine("<div class=\"node bitfield collapsible collapsed\">");
        sb.Append("  <div class=\"header\" onclick=\"toggle(this)\">");
        sb.Append("<span class=\"toggle\">▶</span>");
        sb.Append("<span class=\"name\">").Append(E(node.Name)).Append("</span>");
        sb.Append(" <span class=\"meta\">[0x").Append(node.Offset.ToString("X8")).Append("] (")
            .Append(node.Size).Append(" bytes)</span>: ");
        sb.Append("<span class=\"value int\">0x").Append(node.RawValue.ToString("X")).Append("</span>");
        sb.AppendLine("</div>");
        sb.AppendLine("  <div class=\"children\" style=\"display:none\">");
        foreach (var field in node.Fields)
        {
            sb.AppendLine("<div class=\"node integer\">");
            sb.Append("  <span class=\"name\">").Append(E(field.Name)).Append("</span>: ");
            sb.Append("<span class=\"value int\">").Append(field.Value).Append("</span>");
            if (field.BitHigh == field.BitLow)
                sb.Append(" <span class=\"meta\">(bit ").Append(field.BitLow).Append(")</span>");
            else
                sb.Append(" <span class=\"meta\">(bits ").Append(field.BitHigh).Append(':').Append(field.BitLow).Append(")</span>");
            if (field.EnumLabel is not null)
            {
                sb.Append(" <span class=\"value enum\">\"").Append(E(field.EnumLabel)).Append("\"</span>");
                if (field.EnumDescription is not null)
                    sb.Append(" <span class=\"desc\">- ").Append(E(field.EnumDescription)).Append("</span>");
            }
            sb.AppendLine();
            sb.AppendLine("</div>");
        }
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");
    }

    private static void WriteCompressedNode(StringBuilder sb, DecodedCompressed node, int depth)
    {
        if (node.DecodedContent is not null)
        {
            sb.AppendLine("<div class=\"node compressed collapsible collapsed\">");
            sb.Append("  <div class=\"header\" onclick=\"toggle(this)\">");
            sb.Append("<span class=\"toggle\">▶</span>");
            sb.Append("<span class=\"name\">").Append(E(node.Name)).Append("</span>");
            sb.Append(" <span class=\"meta\">[").Append(E(node.Algorithm)).Append("] (")
                .Append(node.CompressedSize).Append(" → ").Append(node.DecompressedSize).Append(" bytes)</span>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <div class=\"children\" style=\"display:none\">");
            foreach (var child in node.DecodedContent.Children)
                WriteNode(sb, child, depth + 1, false);
            sb.AppendLine("  </div>");
            sb.AppendLine("</div>");
        }
        else
        {
            sb.AppendLine("<div class=\"node compressed\">");
            sb.Append("  <span class=\"name\">").Append(E(node.Name)).Append("</span>");
            sb.Append(" <span class=\"meta\">[").Append(E(node.Algorithm)).Append("] (")
                .Append(node.CompressedSize).Append(" → ").Append(node.DecompressedSize).Append(" bytes)</span>");
            if (node.RawDecompressed is { } raw)
            {
                sb.Append(": <span class=\"value hex\">");
                var span = raw.Span;
                var displayCount = Math.Min(span.Length, 16);
                for (var i = 0; i < displayCount; i++)
                {
                    if (i > 0) sb.Append(' ');
                    sb.Append(span[i].ToString("X2"));
                }
                if (span.Length > 16) sb.Append(" ...");
                sb.Append("</span>");
            }
            sb.AppendLine();
            sb.AppendLine("</div>");
        }
    }

    private static void WriteStyle(StringBuilder sb)
    {
        sb.AppendLine("<style>");
        sb.AppendLine("""
            * { margin: 0; padding: 0; box-sizing: border-box; }
            body { background: #1e1e1e; color: #d4d4d4; font-family: 'Consolas', 'Courier New', monospace; font-size: 14px; padding: 16px; }
            .toolbar { margin-bottom: 12px; }
            .toolbar button { background: #333; color: #d4d4d4; border: 1px solid #555; padding: 4px 12px; margin-right: 8px; cursor: pointer; font-family: inherit; font-size: 13px; }
            .toolbar button:hover { background: #444; }
            .tree { padding-left: 0; }
            .node { padding: 1px 0 1px 20px; position: relative; }
            .node.collapsible > .header { cursor: pointer; user-select: none; }
            .node.collapsible > .header:hover { background: #2a2a2a; }
            .toggle { display: inline-block; width: 16px; font-size: 10px; color: #888; }
            .name { color: #d4d4d4; }
            .meta { color: #888; }
            .desc { color: #aaa; }
            .value.int { color: #4ec9b0; }
            .value.str { color: #6a9955; }
            .value.hex { color: #dcdcaa; }
            .value.enum { color: #c586c0; }
            .valid { color: #4ec9b0; font-weight: bold; }
            .invalid { color: #f44747; font-weight: bold; }
            .children { padding-left: 4px; border-left: 1px solid #333; margin-left: 7px; }
            """);
        sb.AppendLine("</style>");
    }

    private static void WriteScript(StringBuilder sb)
    {
        sb.AppendLine("<script>");
        sb.AppendLine("""
            function toggle(el) {
              const node = el.parentElement;
              const children = node.querySelector('.children');
              const icon = el.querySelector('.toggle');
              if (!children) return;
              if (children.style.display === 'none') {
                children.style.display = '';
                icon.textContent = '▼';
                node.classList.remove('collapsed');
              } else {
                children.style.display = 'none';
                icon.textContent = '▶';
                node.classList.add('collapsed');
              }
            }
            function expandAll() {
              document.querySelectorAll('.collapsible').forEach(n => {
                const ch = n.querySelector('.children');
                const ic = n.querySelector('.toggle');
                if (ch) { ch.style.display = ''; n.classList.remove('collapsed'); }
                if (ic) ic.textContent = '▼';
              });
            }
            function collapseAll() {
              document.querySelectorAll('.collapsible').forEach(n => {
                const ch = n.querySelector('.children');
                const ic = n.querySelector('.toggle');
                if (ch) { ch.style.display = 'none'; n.classList.add('collapsed'); }
                if (ic) ic.textContent = '▶';
              });
            }
            """);
        sb.AppendLine("</script>");
    }
}
