using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Presentation;
using Terminal.Gui.Views;

namespace BinAnalyzer.Tui;

/// <summary>Terminal.Gui の TreeView 用アダプタ。走査ロジックは <see cref="NodeChildren"/> に委譲する。</summary>
internal sealed class DecodedNodeTreeBuilder : ITreeBuilder<DecodedNode>
{
    public bool SupportsCanExpand => true;

    public bool CanExpand(DecodedNode toExpand) => NodeChildren.HasChildren(toExpand);

    public IEnumerable<DecodedNode> GetChildren(DecodedNode forObject) => NodeChildren.Of(forObject);
}
