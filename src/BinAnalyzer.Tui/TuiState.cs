using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Presentation;

namespace BinAnalyzer.Tui;

internal sealed class TuiState
{
    private DecodedNode? _selectedNode;
    private List<DecodedNode> _searchResults = [];
    private int _searchIndex = -1;

    public DecodedNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode != value)
            {
                _selectedNode = value;
                SelectedNodeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public IReadOnlyList<DecodedNode> SearchResults => _searchResults;
    public int SearchIndex => _searchIndex;

    public event EventHandler? SelectedNodeChanged;
    public event EventHandler? SearchResultsChanged;

    public void Search(DecodedNode root, string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            _searchResults = [];
            _searchIndex = -1;
        }
        else
        {
            _searchResults = NodeSearch.ByName(root, query);
            _searchIndex = _searchResults.Count > 0 ? 0 : -1;
        }
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    public DecodedNode? NextSearchResult()
    {
        if (_searchResults.Count == 0) return null;
        _searchIndex = (_searchIndex + 1) % _searchResults.Count;
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
        return _searchResults[_searchIndex];
    }

    public DecodedNode? PreviousSearchResult()
    {
        if (_searchResults.Count == 0) return null;
        _searchIndex = (_searchIndex - 1 + _searchResults.Count) % _searchResults.Count;
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
        return _searchResults[_searchIndex];
    }
}
