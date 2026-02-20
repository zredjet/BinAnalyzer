using BinAnalyzer.Core.Decoded;

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
            _searchResults = CollectMatches(root, query);
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

    internal static List<DecodedNode> CollectMatches(DecodedNode node, string query)
    {
        var results = new List<DecodedNode>();
        CollectMatchesRecursive(node, query, results);
        return results;
    }

    private static void CollectMatchesRecursive(DecodedNode node, string query, List<DecodedNode> results)
    {
        if (node.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            results.Add(node);

        switch (node)
        {
            case DecodedStruct s:
                foreach (var child in s.Children)
                    CollectMatchesRecursive(child, query, results);
                break;
            case DecodedArray a:
                foreach (var element in a.Elements)
                    CollectMatchesRecursive(element, query, results);
                break;
            case DecodedCompressed c when c.DecodedContent is not null:
                foreach (var child in c.DecodedContent.Children)
                    CollectMatchesRecursive(child, query, results);
                break;
        }
    }
}
