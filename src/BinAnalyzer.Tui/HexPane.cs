using System.Text;
using Terminal.Gui;

namespace BinAnalyzer.Tui;

internal sealed class HexPane : FrameView
{
    private readonly ReadOnlyMemory<byte> _data;
    private readonly TextView _textView;
    private readonly TuiState _state;

    public HexPane(ReadOnlyMemory<byte> data, TuiState state)
    {
        Title = "Hex";
        _data = data;
        _state = state;

        _textView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
        };

        Add(_textView);

        _state.SelectedNodeChanged += (_, _) => UpdateHex();
    }

    private void UpdateHex()
    {
        var node = _state.SelectedNode;
        if (node is null)
        {
            _textView.Text = "";
            Title = "Hex";
            return;
        }

        Title = $"Hex \u2014 {node.Name} [0x{node.Offset:X8}] ({node.Size} bytes)";

        var text = GenerateHexDump(_data.Span, node.Offset, node.Size);
        _textView.Text = text;

        // Scroll to the first highlighted line
        _textView.MoveHome();
    }

    internal static string GenerateHexDump(ReadOnlySpan<byte> data, long selectOffset, long selectSize)
    {
        var sb = new StringBuilder();

        // Show a window around the selection: +-2KB
        var windowStart = Math.Max(0, selectOffset - 2048);
        windowStart = (windowStart / 16) * 16; // align to 16-byte boundary
        var windowEnd = Math.Min(data.Length, selectOffset + selectSize + 2048);

        // Header
        sb.AppendLine("Offset    00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F  ASCII");
        sb.AppendLine("\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500  \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500  \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500  \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500");

        var selectEnd = selectOffset + selectSize;

        for (var lineOffset = windowStart; lineOffset < windowEnd; lineOffset += 16)
        {
            var lineEnd = (int)Math.Min(lineOffset + 16, data.Length);
            var count = lineEnd - (int)lineOffset;

            // Offset column
            sb.Append(lineOffset.ToString("X8"));
            sb.Append("  ");

            // Hex bytes
            var hasHighlight = false;
            for (var i = 0; i < 16; i++)
            {
                if (i == 8) sb.Append(' ');
                if (i < count)
                {
                    var byteOffset = lineOffset + i;
                    sb.Append(data[(int)byteOffset].ToString("X2"));
                    sb.Append(' ');
                    if (byteOffset >= selectOffset && byteOffset < selectEnd)
                        hasHighlight = true;
                }
                else
                {
                    sb.Append("   ");
                }
            }

            sb.Append(' ');

            // ASCII column
            for (var i = 0; i < 16; i++)
            {
                if (i < count)
                {
                    var b = data[(int)(lineOffset + i)];
                    sb.Append(b is >= 0x20 and <= 0x7E ? (char)b : '.');
                }
                else
                {
                    sb.Append(' ');
                }
            }

            sb.AppendLine();

            // Marker line for highlighted bytes
            if (hasHighlight)
            {
                sb.Append("          "); // 8 hex offset + 2 spaces
                for (var i = 0; i < 16; i++)
                {
                    if (i == 8) sb.Append(' ');
                    var byteOffset = lineOffset + i;
                    if (i < count && byteOffset >= selectOffset && byteOffset < selectEnd)
                        sb.Append("^^ ");
                    else
                        sb.Append("   ");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
