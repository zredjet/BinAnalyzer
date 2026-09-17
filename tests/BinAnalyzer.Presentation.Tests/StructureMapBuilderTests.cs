using BinAnalyzer.Core.Decoded;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Presentation.Tests;

public sealed class StructureMapBuilderTests
{
    [Fact]
    public void ArrayElements_BecomeSegments_WithTagLabels()
    {
        var index = NodeIndex.Build(PngLikeTree.Build());
        var map = StructureMapBuilder.Build(index, 45);

        map.TotalSize.Should().Be(45);
        map.Segments.Select(s => s.Label).Should().Equal("signature", "IHDR", "IEND");
        map.Segments.Select(s => s.Size).Should().Equal(8, 25, 12);
        map.Segments[0].Bands.Should().ContainSingle().Which.Kind.Should().Be(FieldKind.Magic);
        map.Segments[1].Bands.Select(b => b.Kind).Should().Equal(FieldKind.Len, FieldKind.Tag, FieldKind.Struct, FieldKind.Crc);
    }

    [Fact]
    public void BandSizes_SumToSegmentSize()
    {
        var index = NodeIndex.Build(PngLikeTree.Build());
        foreach (var seg in StructureMapBuilder.Build(index, 45).Segments)
            seg.Bands.Sum(b => b.Size).Should().Be(seg.Size);
    }

    [Fact]
    public void HugeArray_IsCapped_WithRemainderSegment()
    {
        var elements = Enumerable.Range(0, 300)
            .Select(i => (DecodedNode)new DecodedInteger { Name = "e", Offset = i, Size = 1, Value = i }).ToList();
        var root = new DecodedStruct
        {
            Name = "r", StructType = "R", Offset = 0, Size = 300,
            Children = [new DecodedArray { Name = "items", Offset = 0, Size = 300, Elements = elements }],
        };
        var map = StructureMapBuilder.Build(NodeIndex.Build(root), 300);
        map.Segments.Should().HaveCount(StructureMapBuilder.MaxSegments + 1);
        map.Segments[^1].Label.Should().Be("+44");
        map.Segments[^1].Size.Should().Be(44);
        map.Segments.Sum(s => s.Size).Should().Be(300);
    }
}
