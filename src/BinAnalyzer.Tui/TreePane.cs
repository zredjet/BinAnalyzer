using BinAnalyzer.Core.Decoded;
using Terminal.Gui;

namespace BinAnalyzer.Tui;

internal sealed class TreePane : FrameView
{
    private readonly TreeView<DecodedNode> _treeView;

    public TreePane(DecodedStruct root, TuiState state)
    {
        Title = "Tree";

        _treeView = new TreeView<DecodedNode>
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            TreeBuilder = new DecodedNodeTreeBuilder(),
            AspectGetter = DecodedNodeTreeBuilder.GetDisplayText,
        };

        _treeView.AddObject(root);
        _treeView.Expand(root);

        _treeView.SelectionChanged += (sender, args) =>
        {
            state.SelectedNode = _treeView.SelectedObject;
        };

        Add(_treeView);
    }

    public void GoTo(DecodedNode node)
    {
        _treeView.GoTo(node);
    }

    public void ExpandAll() => _treeView.ExpandAll();

    public void CollapseAll() => _treeView.CollapseAll();
}
