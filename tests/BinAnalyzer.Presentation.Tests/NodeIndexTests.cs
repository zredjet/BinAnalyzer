using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Engine;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Presentation.Tests;

public sealed class NodeIndexTests
{
    private readonly DecodedStruct _root = PngLikeTree.Build();
    private readonly NodeIndex _index;

    public NodeIndexTests() => _index = NodeIndex.Build(_root);

    [Fact]
    public void Ids_ArePreOrder_RootIsZero()
    {
        _index.IdOf(_root).Should().Be(0);
        _index.ById(1).Name.Should().Be("signature");
        _index.ById(2).Name.Should().Be("chunks");
        _index.ById(3).Name.Should().Be("chunk");
        _index.ById(4).Name.Should().Be("length");
        _index.Count.Should().Be(19);
        _index.FieldCount.Should().Be(18);
    }

    [Fact]
    public void PathOf_MatchesDiffEnginePaths()
    {
        var width = ((DecodedStruct)((DecodedStruct)((DecodedArray)_root.Children[1]).Elements[0]).Children[2]).Children[0];
        _index.PathOf(width).Should().Be("chunks[0].data.width");
        _index.PathOf(_root).Should().Be("");
        _index.PathOf(_root.Children[0]).Should().Be("signature");
        _index.PathOf(((DecodedArray)_root.Children[1]).Elements[1]).Should().Be("chunks[1]");

        // DiffEngine が返すパスで逆引きできること
        var modified = PngLikeTree.Build();
        var diff = DiffEngine.Compare(_root, modified);
        diff.HasDifferences.Should().BeFalse();
        _index.ByPath("chunks[1].crc").Should().NotBeNull();
        _index.ByPath("chunks[1].crc")!.Name.Should().Be("crc");
    }

    [Fact]
    public void ByPath_RoundTrip_ForAllNodes()
    {
        foreach (var node in _index.Nodes)
            _index.ByPath(_index.PathOf(node)).Should().BeSameAs(node);
    }

    [Fact]
    public void AncestorIdPath_And_IsAncestorOrSelf()
    {
        var crc0 = _index.ByPath("chunks[0].crc")!;
        var id = _index.IdOf(crc0);
        _index.AncestorIdPath(id).Should().Be("/0/2/3/" + id + "/");
        _index.IsAncestorOrSelf(0, id).Should().BeTrue();
        _index.IsAncestorOrSelf(3, id).Should().BeTrue();
        _index.IsAncestorOrSelf(id, id).Should().BeTrue();
        _index.IsAncestorOrSelf(1, id).Should().BeFalse();
        _index.IsAncestorOrSelf(_index.ByPath("chunks[1]")!, crc0).Should().BeFalse();
    }

    [Fact]
    public void ParentOf_And_ElementIndexOf()
    {
        var chunk1 = _index.ByPath("chunks[1]")!;
        _index.ParentOf(chunk1)!.Name.Should().Be("chunks");
        _index.ElementIndexOf(_index.IdOf(chunk1)).Should().Be(1);
        _index.ElementIndexOf(_index.IdOf(_root.Children[0])).Should().BeNull();
        _index.ParentOf(_root).Should().BeNull();
    }

    [Fact]
    public void Leaves_AreSortedByOffset_AndCoverFile()
    {
        _index.Leaves.Should().BeInAscendingOrder(l => l.Offset);
        _index.Leaves.Sum(l => l.Size).Should().Be(45);
        _index.Leaves[0].Kind.Should().Be(FieldKind.Magic);
        _index.Leaves[1].Kind.Should().Be(FieldKind.Len);
        _index.Leaves[2].Kind.Should().Be(FieldKind.Tag);
    }

    [Fact]
    public void LeafAt_Boundaries()
    {
        _index.LeafAt(0)!.Value.Id.Should().Be(1);
        _index.LeafAt(7)!.Value.Id.Should().Be(1);
        _index.LeafAt(8)!.Value.Kind.Should().Be(FieldKind.Len);
        _index.LeafAt(16)!.Value.Id.Should().Be(_index.IdOf(_index.ByPath("chunks[0].data.width")!));
        _index.LeafAt(44)!.Value.Kind.Should().Be(FieldKind.Crc);
        _index.LeafAt(45).Should().BeNull();
        _index.LeafAt(-1).Should().BeNull();
    }

    [Fact]
    public void LeafAt_Gap_ReturnsNull()
    {
        var root = new DecodedStruct
        {
            Name = "r", StructType = "R", Offset = 0, Size = 10,
            Children =
            [
                new DecodedInteger { Name = "a", Offset = 0, Size = 2, Value = 0 },
                new DecodedInteger { Name = "b", Offset = 6, Size = 2, Value = 0 },
            ],
        };
        var idx = NodeIndex.Build(root);
        idx.LeafAt(1)!.Value.Id.Should().Be(1);
        idx.LeafAt(3).Should().BeNull();
        idx.LeafAt(6)!.Value.Id.Should().Be(2);
        idx.FirstLeafIndexAtOrAfter(3).Should().Be(1);
        idx.FirstLeafIndexAtOrAfter(7).Should().Be(2);
    }

    [Fact]
    public void LeafAt_BitOffsetSiblings_FirstWins()
    {
        var root = new DecodedStruct
        {
            Name = "r", StructType = "R", Offset = 0, Size = 1,
            Children =
            [
                new DecodedInteger { Name = "hi", Offset = 0, Size = 1, Value = 0, BitOffset = 0 },
                new DecodedInteger { Name = "lo", Offset = 0, Size = 1, Value = 0, BitOffset = 4 },
            ],
        };
        var idx = NodeIndex.Build(root);
        idx.LeafAt(0)!.Value.Id.Should().Be(1);
        idx.Leaves.Should().HaveCount(2);
    }

    [Fact]
    public void Compressed_ContentIsIndexed_ButNotInFileSpace()
    {
        var inner = new DecodedInteger { Name = "v", Offset = 0, Size = 4, Value = 7 };
        var zip = new DecodedCompressed
        {
            Name = "data", Offset = 4, Size = 6, Algorithm = "zlib", CompressedSize = 6, DecompressedSize = 4,
            DecodedContent = new DecodedStruct { Name = "content", StructType = "inner", Offset = 0, Size = 4, Children = [inner] },
        };
        var root = new DecodedStruct
        {
            Name = "r", StructType = "R", Offset = 0, Size = 10,
            Children = [new DecodedInteger { Name = "len", Offset = 0, Size = 4, Value = 6 }, zip],
        };
        var idx = NodeIndex.Build(root);
        idx.PathOf(inner).Should().Be("data.v");
        idx.IsInFileSpace(idx.IdOf(inner)).Should().BeFalse();
        idx.Leaves.Should().HaveCount(2);
        idx.LeafAt(5)!.Value.Kind.Should().Be(FieldKind.Zip);
        idx.LeafAt(0)!.Value.Kind.Should().Be(FieldKind.Len);
    }

    [Fact]
    public void FindByPathPattern_UsesPathFilterSyntax()
    {
        _index.FindByPathPattern("**.width").Select(n => n.Name).Should().Equal("width");
        _index.FindByPathPattern("**.crc").Should().HaveCount(2);
        _index.FindByPathPattern("chunks[0].*").Should().HaveCount(4);
        _index.FindByPathPattern("nope").Should().BeEmpty();
    }

    [Fact]
    public void Padding_IsNotIndexed_AndVirtualIsNotALeaf()
    {
        var root = new DecodedStruct
        {
            Name = "r", StructType = "R", Offset = 0, Size = 4,
            Children =
            [
                new DecodedInteger { Name = "a", Offset = 0, Size = 2, Value = 0 },
                new DecodedBytes { Name = "pad", Offset = 2, Size = 2, RawBytes = new byte[2], IsPadding = true },
                new DecodedVirtual { Name = "calc", Offset = 4, Size = 0, Value = 1 },
            ],
        };
        var idx = NodeIndex.Build(root);
        idx.Count.Should().Be(3);
        idx.Leaves.Should().HaveCount(1);
        idx.LeafAt(2).Should().BeNull();
    }
}
