using BinAnalyzer.Core.Decoded;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Presentation.Tests;

public sealed class TreeRowBuilderTests
{
    private readonly NodeIndex _index = NodeIndex.Build(PngLikeTree.Build());

    [Fact]
    public void CollapsedRoot_YieldsOnlyRoot()
    {
        var rows = TreeRowBuilder.Build(_index, _ => false, _ => 500);
        rows.Should().HaveCount(1);
        rows[0].Should().Be(new TreeRow(0, 0, true, false, 0));
    }

    [Fact]
    public void ExpandedRoot_ListsDirectChildren_InDocumentOrder()
    {
        var rows = TreeRowBuilder.Build(_index, id => id == 0, _ => 500);
        rows.Select(r => _index.ById(r.Id).Name).Should().Equal("PNG", "signature", "chunks");
        rows.Select(r => r.Depth).Should().Equal(0, 1, 1);
        rows[1].HasChildren.Should().BeFalse();
        rows[2].HasChildren.Should().BeTrue();
        rows[2].Expanded.Should().BeFalse();
    }

    [Fact]
    public void FullyExpanded_MatchesPreOrderIds()
    {
        var rows = TreeRowBuilder.Build(_index, _ => true, _ => 500);
        rows.Should().HaveCount(_index.Count);
        rows.Select(r => r.Id).Should().Equal(Enumerable.Range(0, _index.Count));
        rows.Select(r => r.Depth).Should().Equal(Enumerable.Range(0, _index.Count).Select(_index.DepthOf));
        rows.Should().OnlyContain(r => !r.IsMore);
    }

    [Fact]
    public void Limit_AddsMoreRow_ForRemainingChildren()
    {
        var chunks = _index.IdOf(_index.ByPath("chunks")!);
        var rows = TreeRowBuilder.Build(_index, id => id == 0 || id == chunks, id => id == chunks ? 1 : 500);
        var more = rows.Single(r => r.IsMore);
        more.Id.Should().Be(chunks, "「さらに表示」の行は親のノード ID を持つ");
        more.MoreRemaining.Should().Be(1);
        more.Depth.Should().Be(2);
        rows.Count(r => !r.IsMore && _index.ParentIdOf(r.Id) == chunks).Should().Be(1);
        rows.IndexOf(more).Should().Be(rows.Count - 1);
    }

    [Fact]
    public void IndexOf_FindsRow_IgnoringMoreRows()
    {
        var chunks = _index.IdOf(_index.ByPath("chunks")!);
        var rows = TreeRowBuilder.Build(_index, id => id == 0 || id == chunks, id => id == chunks ? 1 : 500);
        var chunk0 = _index.IdOf(_index.ByPath("chunks[0]")!);
        TreeRowBuilder.IndexOf(rows, chunk0).Should().Be(3);
        TreeRowBuilder.IndexOf(rows, _index.IdOf(_index.ByPath("chunks[1]")!)).Should().Be(-1, "ページ外の要素は行に無い");
        TreeRowBuilder.IndexOf(rows, chunks).Should().Be(2, "IsMore の行（同じ ID）ではなく本体の行");
    }

    [Fact]
    public void LargeArray_IsBoundedByLimit()
    {
        var elements = Enumerable.Range(0, 5_000)
            .Select(i => (DecodedNode)new DecodedInteger { Name = "v", Offset = i, Size = 1, Value = i })
            .ToList();
        var root = new DecodedStruct
        {
            Name = "R", StructType = "r", Offset = 0, Size = 5_000,
            Children = [new DecodedArray { Name = "items", Offset = 0, Size = 5_000, Elements = elements }],
        };
        var index = NodeIndex.Build(root);
        var rows = TreeRowBuilder.Build(index, _ => true, _ => 500);
        rows.Should().HaveCount(1 + 1 + 500 + 1);
        rows[^1].IsMore.Should().BeTrue();
        rows[^1].MoreRemaining.Should().Be(4_500);
    }
}
