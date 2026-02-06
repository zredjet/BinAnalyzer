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

    private static string SearchAttr(string searchable)
        => $" data-searchable=\"{E(searchable)}\"";

    private static void WriteToolbar(StringBuilder sb)
    {
        sb.AppendLine("<div class=\"toolbar\">");
        sb.AppendLine("  <button onclick=\"expandAll()\">全展開</button>");
        sb.AppendLine("  <button onclick=\"collapseAll()\">全折りたたみ</button>");
        sb.AppendLine("  <div class=\"search-bar\">");
        sb.AppendLine("    <input type=\"text\" id=\"search-input\" placeholder=\"検索...\" oninput=\"onSearch()\" />");
        sb.AppendLine("    <span id=\"search-count\"></span>");
        sb.AppendLine("    <button onclick=\"jumpPrev()\" id=\"btn-prev\" disabled>&#x25B2;</button>");
        sb.AppendLine("    <button onclick=\"jumpNext()\" id=\"btn-next\" disabled>&#x25BC;</button>");
        sb.AppendLine("  </div>");
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
            case DecodedVirtual virtualNode:
                WriteVirtualNode(sb, virtualNode);
                break;
            case DecodedError errorNode:
                WriteErrorNode(sb, errorNode);
                break;
        }
    }

    private static void WriteStructNode(StringBuilder sb, DecodedStruct node, int depth, bool isRoot)
    {
        var collapsed = depth > 1 ? " collapsed" : "";
        var searchText = node.StructType != node.Name
            ? $"{node.Name} {node.StructType}"
            : node.Name;
        sb.Append("<div class=\"node struct collapsible").Append(collapsed).Append('"')
            .Append(SearchAttr(searchText)).AppendLine(">");
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
        sb.Append("<div class=\"node array collapsible").Append(collapsed).Append('"')
            .Append(SearchAttr(node.Name)).AppendLine(">");
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
                var searchText = structElement.StructType != structElement.Name
                    ? $"#{i} {structElement.StructType}"
                    : $"#{i}";
                sb.Append("<div class=\"node struct collapsible collapsed\"")
                    .Append(SearchAttr(searchText)).AppendLine(">");
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
        var searchParts = new StringBuilder();
        searchParts.Append(node.Name).Append(' ').Append(node.Value);
        if (node.Value is >= 16 or <= -16)
            searchParts.Append(" 0x").Append(node.Value.ToString("X"));
        if (node.EnumLabel is not null)
            searchParts.Append(' ').Append(node.EnumLabel);
        sb.Append("<div class=\"node integer\"").Append(SearchAttr(searchParts.ToString())).AppendLine(">");
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
        AppendValidationHtml(sb, node);
        sb.AppendLine();
        sb.AppendLine("</div>");
    }

    private static void AppendValidationHtml(StringBuilder sb, DecodedNode node)
    {
        if (node.Validation is { } v)
        {
            sb.Append(v.Passed
                ? " <span class=\"valid\">✓</span>"
                : " <span class=\"invalid\">✗</span>");
        }
    }

    private static void WriteBytesNode(StringBuilder sb, DecodedBytes node)
    {
        var searchParts = new StringBuilder();
        searchParts.Append(node.Name);
        var span = node.RawBytes.Span;
        var displayCount = Math.Min(span.Length, 16);
        for (var i = 0; i < displayCount; i++)
            searchParts.Append(' ').Append(span[i].ToString("X2"));
        sb.Append("<div class=\"node bytes\"").Append(SearchAttr(searchParts.ToString())).AppendLine(">");
        sb.Append("  <span class=\"name\">").Append(E(node.Name)).Append("</span>");
        sb.Append(" <span class=\"meta\">[0x").Append(node.Offset.ToString("X8")).Append("] (")
            .Append(node.Size).Append(" bytes)</span>: ");

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
        sb.Append("<div class=\"node string\"").Append(SearchAttr($"{node.Name} {node.Value}")).AppendLine(">");
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
        sb.Append("<div class=\"node float\"").Append(SearchAttr($"{node.Name} {node.Value:G}")).AppendLine(">");
        sb.Append("  <span class=\"name\">").Append(E(node.Name)).Append("</span>: ");
        sb.Append("<span class=\"value int\">").Append(node.Value.ToString("G")).Append("</span>");
        sb.AppendLine();
        sb.AppendLine("</div>");
    }

    private static void WriteFlagsNode(StringBuilder sb, DecodedFlags node)
    {
        var searchParts = new StringBuilder();
        searchParts.Append(node.Name).Append(" 0x").Append(node.RawValue.ToString("X"));
        foreach (var flag in node.FlagStates)
            searchParts.Append(' ').Append(flag.Name).Append('=').Append(flag.Meaning ?? (flag.IsSet ? "set" : "clear"));
        sb.Append("<div class=\"node flags\"").Append(SearchAttr(searchParts.ToString())).AppendLine(">");
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
        sb.Append("<div class=\"node bitfield collapsible collapsed\"")
            .Append(SearchAttr($"{node.Name} 0x{node.RawValue:X}")).AppendLine(">");
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
            var searchText = field.EnumLabel is not null
                ? $"{field.Name} {field.Value} {field.EnumLabel}"
                : $"{field.Name} {field.Value}";
            sb.Append("<div class=\"node integer\"").Append(SearchAttr(searchText)).AppendLine(">");
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

    private static void WriteVirtualNode(StringBuilder sb, DecodedVirtual node)
    {
        sb.Append("<div class=\"node virtual\"").Append(SearchAttr($"{node.Name} {node.Value}")).AppendLine(">");
        sb.Append("  <span class=\"name\">").Append(E(node.Name)).Append("</span>: ");
        sb.Append("<span class=\"value int\">= ").Append(E(node.Value.ToString() ?? "")).Append("</span>");
        sb.AppendLine();
        sb.AppendLine("</div>");
    }

    private static void WriteErrorNode(StringBuilder sb, DecodedError node)
    {
        sb.Append("<div class=\"node error\"").Append(SearchAttr($"{node.Name} ERROR {node.ErrorMessage}")).AppendLine(">");
        sb.Append("  <span class=\"invalid\">✗ ").Append(E(node.Name)).Append("</span>");
        sb.Append(" <span class=\"meta\">[ERROR at 0x").Append(node.Offset.ToString("X8")).Append("]: </span>");
        sb.Append("<span class=\"invalid\">").Append(E(node.ErrorMessage)).Append("</span>");
        sb.AppendLine();
        sb.AppendLine("</div>");
    }

    private static void WriteCompressedNode(StringBuilder sb, DecodedCompressed node, int depth)
    {
        if (node.DecodedContent is not null)
        {
            sb.Append("<div class=\"node compressed collapsible collapsed\"")
                .Append(SearchAttr($"{node.Name} {node.Algorithm}")).AppendLine(">");
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
            sb.Append("<div class=\"node compressed\"")
                .Append(SearchAttr($"{node.Name} {node.Algorithm}")).AppendLine(">");
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
            .toolbar { margin-bottom: 12px; display: flex; align-items: center; flex-wrap: wrap; gap: 4px; }
            .toolbar button { background: #333; color: #d4d4d4; border: 1px solid #555; padding: 4px 12px; cursor: pointer; font-family: inherit; font-size: 13px; }
            .toolbar button:hover { background: #444; }
            .search-bar { display: inline-flex; align-items: center; margin-left: 16px; gap: 4px; }
            .search-bar input { background: #333; color: #d4d4d4; border: 1px solid #555; padding: 4px 8px; font-family: inherit; font-size: 13px; width: 200px; }
            .search-bar input:focus { border-color: #007acc; outline: none; }
            #search-count { color: #888; font-size: 12px; min-width: 48px; }
            .search-bar button { padding: 2px 8px; font-size: 11px; }
            .search-bar button:disabled { color: #555; cursor: default; }
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
            .search-match { outline: 1px solid #e2c56b; background: rgba(90, 74, 0, 0.4); }
            .search-focus { outline: 2px solid #007acc; background: rgba(0, 122, 204, 0.3); }
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

            // --- Search ---
            let matches = [];
            let currentIndex = -1;
            let savedState = null;

            function saveCollapseState() {
              savedState = new Map();
              document.querySelectorAll('.collapsible').forEach(n => {
                savedState.set(n, n.classList.contains('collapsed'));
              });
            }
            function restoreCollapseState() {
              if (!savedState) return;
              savedState.forEach((wasCollapsed, n) => {
                const ch = n.querySelector('.children');
                const ic = n.querySelector('.toggle');
                if (!ch) return;
                if (wasCollapsed) {
                  ch.style.display = 'none';
                  ic && (ic.textContent = '▶');
                  n.classList.add('collapsed');
                } else {
                  ch.style.display = '';
                  ic && (ic.textContent = '▼');
                  n.classList.remove('collapsed');
                }
              });
              savedState = null;
            }
            function expandAncestors(el) {
              let p = el.parentElement;
              while (p) {
                if (p.classList && p.classList.contains('children')) {
                  p.style.display = '';
                  const parent = p.parentElement;
                  if (parent && parent.classList.contains('collapsible')) {
                    parent.classList.remove('collapsed');
                    const ic = parent.querySelector(':scope > .header > .toggle');
                    if (ic) ic.textContent = '▼';
                  }
                }
                p = p.parentElement;
              }
            }
            function clearHighlights() {
              document.querySelectorAll('.search-match, .search-focus').forEach(n => {
                n.classList.remove('search-match', 'search-focus');
              });
            }
            function updateCounter() {
              const el = document.getElementById('search-count');
              const prevBtn = document.getElementById('btn-prev');
              const nextBtn = document.getElementById('btn-next');
              if (matches.length === 0) {
                const q = document.getElementById('search-input').value;
                el.textContent = q ? '0 件' : '';
                prevBtn.disabled = true;
                nextBtn.disabled = true;
              } else {
                el.textContent = (currentIndex + 1) + ' / ' + matches.length + ' 件';
                prevBtn.disabled = false;
                nextBtn.disabled = false;
              }
            }
            function onSearch() {
              const query = document.getElementById('search-input').value.toLowerCase().trim();
              clearHighlights();
              matches = [];
              currentIndex = -1;
              if (!query) {
                restoreCollapseState();
                updateCounter();
                return;
              }
              if (!savedState) saveCollapseState();
              const nodes = document.querySelectorAll('.node[data-searchable]');
              nodes.forEach(n => {
                const s = n.getAttribute('data-searchable').toLowerCase();
                if (s.indexOf(query) !== -1) {
                  matches.push(n);
                  n.classList.add('search-match');
                  expandAncestors(n);
                }
              });
              if (matches.length > 0) {
                currentIndex = 0;
                matches[0].classList.add('search-focus');
                matches[0].scrollIntoView({ block: 'center', behavior: 'smooth' });
              }
              updateCounter();
            }
            function jumpNext() {
              if (matches.length === 0) return;
              matches[currentIndex].classList.remove('search-focus');
              currentIndex = (currentIndex + 1) % matches.length;
              matches[currentIndex].classList.add('search-focus');
              matches[currentIndex].scrollIntoView({ block: 'center', behavior: 'smooth' });
              updateCounter();
            }
            function jumpPrev() {
              if (matches.length === 0) return;
              matches[currentIndex].classList.remove('search-focus');
              currentIndex = (currentIndex - 1 + matches.length) % matches.length;
              matches[currentIndex].classList.add('search-focus');
              matches[currentIndex].scrollIntoView({ block: 'center', behavior: 'smooth' });
              updateCounter();
            }
            function clearSearch() {
              document.getElementById('search-input').value = '';
              clearHighlights();
              matches = [];
              currentIndex = -1;
              restoreCollapseState();
              updateCounter();
            }
            document.addEventListener('keydown', function(e) {
              const input = document.getElementById('search-input');
              const isInputFocused = document.activeElement === input;
              if (e.key === '/' && !isInputFocused) {
                e.preventDefault();
                input.focus();
              }
              if (e.key === 'Escape') {
                clearSearch();
                input.blur();
              }
              if (e.key === 'Enter' && isInputFocused) {
                e.preventDefault();
                if (e.shiftKey) jumpPrev(); else jumpNext();
              }
            });
            """);
        sb.AppendLine("</script>");
    }
}
