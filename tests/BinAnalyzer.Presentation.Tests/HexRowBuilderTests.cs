using BinAnalyzer.Core.Decoded;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Presentation.Tests;

public sealed class HexRowBuilderTests
{
    private readonly NodeIndex _index = NodeIndex.Build(PngLikeTree.Build());
    private HexRowBuilder Builder => new(PngLikeTree.Data, _index);

    [Fact]
    public void RowCount_And_LastRowPadding()
    {
        var b = Builder;
        b.RowCount.Should().Be(3);
        var last = b.Build(2);
        last.Offset.Should().Be(32);
        last.Cells.Take(13).Should().AllSatisfy(c => c.Should().NotBeNull());
        last.Cells.Skip(13).Should().AllSatisfy(c => c.Should().BeNull());
        last.Ascii.Should().HaveLength(13);
        HexRowBuilder.RowIndexOf(33).Should().Be(2);
    }

    [Fact]
    public void Cells_CarryKind_StartEnd_AndAncestorPath()
    {
        var row0 = Builder.Build(0);
        var sig0 = row0.Cells[0]!.Value;
        sig0.Value.Should().Be(0x89);
        sig0.Kind.Should().Be(FieldKind.Magic);
        sig0.FieldStart.Should().BeTrue();
        sig0.FieldEnd.Should().BeFalse();
        row0.Cells[7]!.Value.FieldEnd.Should().BeTrue();
        row0.Cells[8]!.Value.Kind.Should().Be(FieldKind.Len);
        row0.Cells[12]!.Value.Kind.Should().Be(FieldKind.Tag);
        row0.Cells[12]!.Value.AncestorPath.Should().Be("/0/2/3/5/");
        row0.Ascii.Should().Be("·PNG········IHDR");
    }

    [Fact]
    public void Field_SpanningRows_HasStartInOneRow_EndInNext()
    {
        // crc of chunk0 is at 29..32: starts row 1 (offset 29), ends row 2 (offset 32)
        var row1 = Builder.Build(1);
        var row2 = Builder.Build(2);
        row1.Cells[13]!.Value.FieldStart.Should().BeTrue();
        row1.Cells[15]!.Value.FieldEnd.Should().BeFalse();
        row2.Cells[0]!.Value.FieldStart.Should().BeFalse();
        row2.Cells[0]!.Value.FieldEnd.Should().BeTrue();
        row2.Cells[0]!.Value.NodeId.Should().Be(row1.Cells[13]!.Value.NodeId);
    }

    [Fact]
    public void Ghosts_ListValuesStartingInRow_MagicAsCheck_CrcAsCheck()
    {
        var row0 = Builder.Build(0);
        row0.Ghosts.Select(g => g.Name).Should().Equal("signature", "length", "type");
        row0.Ghosts[0].Style.Should().Be(GhostStyle.Ok);
        row0.Ghosts[0].ValueText.Should().BeEmpty();
        row0.Ghosts[1].ValueText.Should().Be("13");
        row0.Ghosts[2].ValueText.Should().Be("\"IHDR\"");

        var row1 = Builder.Build(1);
        row1.Ghosts.Should().HaveCount(HexRowBuilder.MaxGhosts);
        row1.Ghosts[0].Name.Should().Be("width");
        row1.Ghosts[3].ValueText.Should().Be("2 \"truecolor\"");

        var row2 = Builder.Build(2);
        row2.Ghosts.Select(g => g.Name).Should().Equal("length", "type", "crc");
        row2.Ghosts[2].Style.Should().Be(GhostStyle.Ng);
    }

    [Fact]
    public void Gap_Bytes_HaveNoNode()
    {
        var root = new DecodedStruct
        {
            Name = "r", StructType = "R", Offset = 0, Size = 4,
            Children = [new DecodedInteger { Name = "a", Offset = 0, Size = 2, Value = 0 }],
        };
        var b = new HexRowBuilder(new byte[] { 1, 2, 3, 4 }, NodeIndex.Build(root));
        var row = b.Build(0);
        row.Cells[2]!.Value.NodeId.Should().Be(-1);
        row.Cells[2]!.Value.Value.Should().Be(3);
        row.Ghosts.Should().HaveCount(1);
    }

    [Fact]
    public void Padding_IsExcludedFromGhosts_LongValuesTruncated()
    {
        var root = new DecodedStruct
        {
            Name = "r", StructType = "R", Offset = 0, Size = 60,
            Children =
            [
                new DecodedBytes { Name = "pad", Offset = 0, Size = 2, RawBytes = new byte[2], IsPadding = true },
                new DecodedString { Name = "s", Offset = 2, Size = 58, Value = new string('x', 100), Encoding = "ASCII" },
            ],
        };
        var b = new HexRowBuilder(new byte[60], NodeIndex.Build(root));
        var row = b.Build(0);
        row.Ghosts.Should().HaveCount(1);
        row.Ghosts[0].ValueText.Should().HaveLength(40).And.EndWith("…");
        row.Cells[0]!.Value.NodeId.Should().Be(-1, "padding は索引に含まれず隙間になる");
    }

    // --- REQ-156: 差分表示中の相手と異なるバイト ---

    [Fact]
    public void WithoutCompare_NoCellDiffers()
    {
        Builder.IsComparing.Should().BeFalse();
        Builder.Build(1).Cells.Should().AllSatisfy(c => c!.Value.Differs.Should().BeFalse());
    }

    [Fact]
    public void WithCompare_MarksDifferingCells_Only()
    {
        var other = (byte[])PngLikeTree.Data.Clone();
        other[0x13] ^= 0xFF;
        var b = new HexRowBuilder(PngLikeTree.Data, _index, other);

        b.IsComparing.Should().BeTrue();
        var row1 = b.Build(1);
        row1.Cells.Select((c, i) => (c!.Value.Differs, i)).Where(x => x.Differs).Select(x => x.i).Should().Equal(3);
        b.Build(0).Cells.Should().AllSatisfy(c => c!.Value.Differs.Should().BeFalse());
    }

    [Fact]
    public void WithShorterCompare_BytesBeyondItsEnd_Differ()
    {
        var shorter = PngLikeTree.Data[..40];
        var last = new HexRowBuilder(PngLikeTree.Data, _index, shorter).Build(2);

        last.Cells.Take(8).Should().AllSatisfy(c => c!.Value.Differs.Should().BeFalse());
        last.Cells.Skip(8).Take(5).Should().AllSatisfy(c => c!.Value.Differs.Should().BeTrue());
    }
}
