using BinAnalyzer.Core.Decoded;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Presentation.Tests;

/// <summary>REQ-177 でパス・祖先列を保持せず親リンクから組み立てるようにした部分の検証。</summary>
public sealed class NodeIndexPathTests
{
    private readonly NodeIndex _index = NodeIndex.Build(PngLikeTree.Build());

    [Theory]
    [InlineData("nope")]
    [InlineData("chunks[5]")]
    [InlineData("chunks[x]")]
    [InlineData("chunks[0")]
    [InlineData("chunks.0")]
    [InlineData("signature[0]")]
    [InlineData("chunks[0]data")]
    [InlineData(".signature")]
    [InlineData("chunks[0].")]
    [InlineData("[0]")]
    public void ByPath_ReturnsNull_ForMalformedOrMissing(string path)
    {
        _index.ByPath(path).Should().BeNull();
        _index.IdByPath(path).Should().Be(-1);
    }

    [Fact]
    public void IdByPath_Root_IsZero()
    {
        _index.IdByPath("").Should().Be(0);
        _index.PathOf(0).Should().Be("");
    }

    [Fact]
    public void OrdinalOf_And_ElementIndexOf()
    {
        var chunk1 = _index.IdOf(_index.ByPath("chunks[1]")!);
        _index.OrdinalOf(chunk1).Should().Be(1);
        _index.ElementIndexOf(chunk1).Should().Be(1);
        var crc = _index.IdOf(_index.ByPath("chunks[1].crc")!);
        _index.OrdinalOf(crc).Should().Be(2, "struct の子列（padding 除外）の中での位置");
        _index.ElementIndexOf(crc).Should().BeNull("配列要素ではない");
        _index.ElementIndexOf(0).Should().BeNull();
    }

    [Fact]
    public void IsAncestorOrSelf_UsesParentChain()
    {
        var chunks = _index.IdOf(_index.ByPath("chunks")!);
        var width = _index.IdOf(_index.ByPath("chunks[0].data.width")!);
        var crc1 = _index.IdOf(_index.ByPath("chunks[1].crc")!);
        _index.IsAncestorOrSelf(chunks, width).Should().BeTrue();
        _index.IsAncestorOrSelf(width, chunks).Should().BeFalse("逆方向");
        _index.IsAncestorOrSelf(width, crc1).Should().BeFalse("ID が小さいだけで祖先ではない");
        _index.IsAncestorOrSelf(-1, width).Should().BeFalse();
        _index.IsAncestorOrSelf(width, -1).Should().BeFalse();
    }

    [Fact]
    public void PaddingChildren_AreSkipped_AndOrdinalsStayDense()
    {
        var root = new DecodedStruct
        {
            Name = "R", StructType = "r", Offset = 0, Size = 4,
            Children =
            [
                new DecodedInteger { Name = "a", Offset = 0, Size = 1, Value = 1 },
                new DecodedBytes { Name = "pad", Offset = 1, Size = 2, RawBytes = new byte[2], IsPadding = true },
                new DecodedInteger { Name = "b", Offset = 3, Size = 1, Value = 2 },
            ],
        };
        var index = NodeIndex.Build(root);
        index.Count.Should().Be(3);
        index.PathOf(2).Should().Be("b");
        index.OrdinalOf(2).Should().Be(1);
        index.ByPath("pad").Should().BeNull();
        index.ByPath("b").Should().BeSameAs(root.Children[2]);
    }

    [Fact]
    public void CompressedContent_PathsAndFileSpace()
    {
        var inner = new DecodedStruct
        {
            Name = "content", StructType = "inner", Offset = 0, Size = 4,
            Children = [new DecodedInteger { Name = "n", Offset = 0, Size = 4, Value = 7 }],
        };
        var root = new DecodedStruct
        {
            Name = "R", StructType = "r", Offset = 0, Size = 10,
            Children =
            [
                new DecodedCompressed { Name = "z", Offset = 0, Size = 10, CompressedSize = 10, DecompressedSize = 4, Algorithm = "zlib", DecodedContent = inner },
            ],
        };
        var index = NodeIndex.Build(root);
        var n = index.IdOf(inner.Children[0]);
        index.PathOf(n).Should().Be("z.n");
        index.ByPath("z.n").Should().BeSameAs(inner.Children[0]);
        index.IsInFileSpace(n).Should().BeFalse();
        index.AncestorIdPath(n).Should().Be("/0/1/2/");
        index.Leaves.Should().ContainSingle().Which.Id.Should().Be(1, "圧縮ノード自身がファイル空間の葉");
    }

    [Fact]
    public void DeepTree_AncestorIdPath_And_Path_Work()
    {
        // 深さ 200 の入れ子（AncestorIdPath の固定バッファ 64 段を超える経路）
        DecodedNode leaf = new DecodedInteger { Name = "v", Offset = 0, Size = 1, Value = 0 };
        var node = leaf;
        for (var d = 0; d < 200; d++)
            node = new DecodedStruct { Name = "s" + d, StructType = "s", Offset = 0, Size = 1, Children = [node] };
        var index = NodeIndex.Build((DecodedStruct)node);
        var id = index.IdOf(leaf);
        index.DepthOf(id).Should().Be(200);
        index.AncestorIdPath(id).Should().Be("/" + string.Join("/", Enumerable.Range(0, 201)) + "/");
        index.PathOf(id).Should().EndWith(".s0.v").And.StartWith("s198.");
        index.ByPath(index.PathOf(id)).Should().BeSameAs(leaf);
    }
}
