using System.Collections.ObjectModel;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace BinAnalyzer.Tui;

internal sealed class DetailPane : FrameView
{
    // ListView is the non-obsolete read-only, scrollable text presenter in Terminal.Gui 2.5
    // (TextView is obsolete there). One item per line.
    private readonly ListView _listView;
    private readonly TuiState _state;

    public DetailPane(TuiState state)
    {
        Title = "Detail";
        _state = state;

        _listView = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        Add(_listView);

        _state.SelectedNodeChanged += (_, _) => UpdateDetail();
    }

    private void UpdateDetail()
    {
        var node = _state.SelectedNode;
        if (node is null)
        {
            _listView.SetSource(new ObservableCollection<string>());
            return;
        }

        var details = NodeDetailFormatter.Format(node);
        var maxKeyLen = details.Max(d => d.Key.Length);
        var lines = details.Select(d => $"{d.Key.PadRight(maxKeyLen)}  {d.Value}");
        _listView.SetSource(new ObservableCollection<string>(lines));
    }
}
