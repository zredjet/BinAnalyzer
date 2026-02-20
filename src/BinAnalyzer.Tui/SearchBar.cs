using BinAnalyzer.Core.Decoded;
using Terminal.Gui;

namespace BinAnalyzer.Tui;

internal sealed class SearchBar : View
{
    private readonly TextField _textField;
    private readonly Label _resultLabel;
    private readonly DecodedStruct _root;
    private readonly TuiState _state;
    private readonly TreePane _treePane;

    public SearchBar(DecodedStruct root, TuiState state, TreePane treePane)
    {
        _root = root;
        _state = state;
        _treePane = treePane;

        var label = new Label
        {
            Text = "Search: ",
            X = 0,
            Y = 0,
            Width = 8,
        };

        _textField = new TextField
        {
            X = 8,
            Y = 0,
            Width = Dim.Fill(20),
        };

        _resultLabel = new Label
        {
            X = Pos.Right(_textField) + 1,
            Y = 0,
            Width = Dim.Fill(),
            Text = "",
        };

        Add(label, _textField, _resultLabel);

        _textField.HasFocusChanged += (_, args) =>
        {
            if (args.NewValue)
                OnSearchTextChanged();
        };

        _textField.Accepting += (_, _) =>
        {
            var next = _state.NextSearchResult();
            if (next is not null)
                _treePane.GoTo(next);
            UpdateResultLabel();
        };

        _state.SearchResultsChanged += (_, _) => UpdateResultLabel();
    }

    private void OnSearchTextChanged()
    {
        // Re-search when focus is gained (text may have changed)
    }

    public void PerformSearch()
    {
        var query = _textField.Text;
        _state.Search(_root, query);
        if (_state.SearchResults.Count > 0)
            _treePane.GoTo(_state.SearchResults[0]);
    }

    private void UpdateResultLabel()
    {
        if (_state.SearchResults.Count == 0)
            _resultLabel.Text = _state.SearchIndex == -1 ? "" : "No matches";
        else
            _resultLabel.Text = $"{_state.SearchIndex + 1}/{_state.SearchResults.Count}";
    }

    public void ShowAndFocus()
    {
        Visible = true;
        _textField.Text = "";
        _textField.SetFocus();
    }

    public void HideBar()
    {
        Visible = false;
        _state.Search(_root, "");
    }
}
