using Terminal.Gui;

namespace BinAnalyzer.Tui;

internal sealed class DetailPane : FrameView
{
    private readonly TextView _textView;
    private readonly TuiState _state;

    public DetailPane(TuiState state)
    {
        Title = "Detail";
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

        _state.SelectedNodeChanged += (_, _) => UpdateDetail();
    }

    private void UpdateDetail()
    {
        var node = _state.SelectedNode;
        if (node is null)
        {
            _textView.Text = "";
            return;
        }

        var details = NodeDetailFormatter.Format(node);
        var maxKeyLen = details.Max(d => d.Key.Length);
        var text = string.Join(Environment.NewLine,
            details.Select(d => $"{d.Key.PadRight(maxKeyLen)}  {d.Value}"));
        _textView.Text = text;
    }
}
