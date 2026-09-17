using BinAnalyzer.Core.Decoded;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Presentation.Tests;

public sealed class BreadcrumbBuilderTests
{
    [Fact]
    public void Build_RootToNode_ArrayElementsAsHashIndex()
    {
        var index = NodeIndex.Build(PngLikeTree.Build());
        var width = index.ByPath("chunks[0].data.width")!;
        var crumbs = BreadcrumbBuilder.Build(width, index, "image.png");
        crumbs.Select(c => c.Label).Should().Equal("image.png", "chunks", "#0", "data", "width");
        crumbs[0].NodeId.Should().Be(0);
        crumbs[^1].NodeId.Should().Be(index.IdOf(width));
    }

    [Fact]
    public void Build_Root_IsSingleItem()
    {
        var index = NodeIndex.Build(PngLikeTree.Build());
        BreadcrumbBuilder.Build(index.Root, index, "f").Should().ContainSingle().Which.Label.Should().Be("f");
    }
}

public sealed class ChecksumSummaryTests
{
    [Fact]
    public void Compute_CountsChecksums_Validations_Compressed_Errors()
    {
        var index = NodeIndex.Build(PngLikeTree.Build());
        var s = ChecksumSummary.Compute(index);
        s.ChecksumTotal.Should().Be(2);
        s.ChecksumValid.Should().Be(1);
        s.AllChecksumsValid.Should().BeFalse();
        s.Checksums.Select(c => c.Path).Should().Equal("chunks[0].crc", "chunks[1].crc");
        s.ValidationTotal.Should().Be(1);
        s.ValidationPassed.Should().Be(1);
        s.CompressedStreams.Should().Be(0);
        s.Errors.Should().Be(0);
    }

    [Fact]
    public void Compute_WithCompressedAndError()
    {
        var root = new DecodedStruct
        {
            Name = "r", StructType = "R", Offset = 0, Size = 10,
            Children =
            [
                new DecodedCompressed { Name = "z", Offset = 0, Size = 5, Algorithm = "zlib", CompressedSize = 5, DecompressedSize = 9 },
                new DecodedError { Name = "e", Offset = 5, Size = 0, ErrorMessage = "boom" },
            ],
        };
        var s = ChecksumSummary.Compute(NodeIndex.Build(root));
        s.CompressedStreams.Should().Be(1);
        s.Errors.Should().Be(1);
        s.ChecksumTotal.Should().Be(0);
        s.AllChecksumsValid.Should().BeTrue();
    }
}
