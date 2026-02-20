using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Tui;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Tui.Tests;

public sealed class DecodedNodeTreeBuilderTests
{
    private readonly DecodedNodeTreeBuilder _builder = new();

    [Fact]
    public void CanExpand_Struct_WithChildren_ReturnsTrue()
    {
        var node = new DecodedStruct
        {
            Name = "root",
            StructType = "Root",
            Offset = 0,
            Size = 10,
            Children =
            [
                new DecodedInteger { Name = "field", Offset = 0, Size = 4, Value = 1 },
            ],
        };

        _builder.CanExpand(node).Should().BeTrue();
    }

    [Fact]
    public void CanExpand_Struct_Empty_ReturnsFalse()
    {
        var node = new DecodedStruct
        {
            Name = "empty",
            StructType = "Empty",
            Offset = 0,
            Size = 0,
            Children = [],
        };

        _builder.CanExpand(node).Should().BeFalse();
    }

    [Fact]
    public void CanExpand_Struct_OnlyPadding_ReturnsFalse()
    {
        var node = new DecodedStruct
        {
            Name = "padded",
            StructType = "Padded",
            Offset = 0,
            Size = 4,
            Children =
            [
                new DecodedBytes { Name = "padding", Offset = 0, Size = 4, RawBytes = new byte[4], IsPadding = true },
            ],
        };

        _builder.CanExpand(node).Should().BeFalse();
    }

    [Fact]
    public void CanExpand_Array_WithElements_ReturnsTrue()
    {
        var node = new DecodedArray
        {
            Name = "items",
            Offset = 0,
            Size = 8,
            Elements =
            [
                new DecodedInteger { Name = "item0", Offset = 0, Size = 4, Value = 1 },
            ],
        };

        _builder.CanExpand(node).Should().BeTrue();
    }

    [Fact]
    public void CanExpand_Integer_ReturnsFalse()
    {
        var node = new DecodedInteger
        {
            Name = "value",
            Offset = 0,
            Size = 4,
            Value = 42,
        };

        _builder.CanExpand(node).Should().BeFalse();
    }

    [Fact]
    public void GetChildren_Struct_ReturnNonPaddingChildren()
    {
        var child1 = new DecodedInteger { Name = "width", Offset = 0, Size = 4, Value = 800 };
        var padding = new DecodedBytes { Name = "pad", Offset = 4, Size = 2, RawBytes = new byte[2], IsPadding = true };
        var child2 = new DecodedInteger { Name = "height", Offset = 6, Size = 4, Value = 600 };

        var node = new DecodedStruct
        {
            Name = "header",
            StructType = "Header",
            Offset = 0,
            Size = 10,
            Children = [child1, padding, child2],
        };

        var children = _builder.GetChildren(node).ToList();

        children.Should().HaveCount(2);
        children.Should().Contain(child1);
        children.Should().Contain(child2);
        children.Should().NotContain(padding);
    }

    [Fact]
    public void GetChildren_Array_ReturnsAllElements()
    {
        var elem1 = new DecodedInteger { Name = "e0", Offset = 0, Size = 4, Value = 1 };
        var elem2 = new DecodedInteger { Name = "e1", Offset = 4, Size = 4, Value = 2 };

        var node = new DecodedArray
        {
            Name = "items",
            Offset = 0,
            Size = 8,
            Elements = [elem1, elem2],
        };

        var children = _builder.GetChildren(node).ToList();

        children.Should().HaveCount(2);
        children.Should().ContainInOrder(elem1, elem2);
    }

    [Fact]
    public void GetChildren_Compressed_WithDecodedContent_ReturnsContentChildren()
    {
        var innerChild = new DecodedInteger { Name = "value", Offset = 0, Size = 4, Value = 42 };
        var decodedContent = new DecodedStruct
        {
            Name = "content",
            StructType = "Content",
            Offset = 0,
            Size = 4,
            Children = [innerChild],
        };

        var node = new DecodedCompressed
        {
            Name = "compressed_data",
            Offset = 0,
            Size = 100,
            Algorithm = "zlib",
            CompressedSize = 100,
            DecompressedSize = 200,
            DecodedContent = decodedContent,
        };

        var children = _builder.GetChildren(node).ToList();

        children.Should().HaveCount(1);
        children.Should().Contain(innerChild);
    }

    [Fact]
    public void GetChildren_Integer_ReturnsEmpty()
    {
        var node = new DecodedInteger
        {
            Name = "value",
            Offset = 0,
            Size = 4,
            Value = 42,
        };

        _builder.GetChildren(node).Should().BeEmpty();
    }

    [Fact]
    public void GetDisplayText_Struct_SameName_ReturnsName()
    {
        var node = new DecodedStruct
        {
            Name = "header",
            StructType = "header",
            Offset = 0,
            Size = 10,
            Children = [],
        };

        DecodedNodeTreeBuilder.GetDisplayText(node).Should().Be("header");
    }

    [Fact]
    public void GetDisplayText_Struct_DifferentType_ReturnsNameAndType()
    {
        var node = new DecodedStruct
        {
            Name = "ihdr",
            StructType = "IHDR",
            Offset = 0,
            Size = 13,
            Children = [],
        };

        DecodedNodeTreeBuilder.GetDisplayText(node).Should().Be("ihdr \u2192 IHDR");
    }

    [Fact]
    public void GetDisplayText_Integer_ReturnsNameAndValue()
    {
        var node = new DecodedInteger
        {
            Name = "width",
            Offset = 0,
            Size = 4,
            Value = 800,
        };

        DecodedNodeTreeBuilder.GetDisplayText(node).Should().Be("width: 800");
    }

    [Fact]
    public void GetDisplayText_Integer_WithEnum_IncludesLabel()
    {
        var node = new DecodedInteger
        {
            Name = "type",
            Offset = 0,
            Size = 1,
            Value = 2,
            EnumLabel = "RGB",
        };

        DecodedNodeTreeBuilder.GetDisplayText(node).Should().Be("type: 2 \"RGB\"");
    }

    [Fact]
    public void GetDisplayText_Array_ShowsCount()
    {
        var node = new DecodedArray
        {
            Name = "chunks",
            Offset = 0,
            Size = 100,
            Elements =
            [
                new DecodedStruct { Name = "c0", StructType = "Chunk", Offset = 0, Size = 50, Children = [] },
                new DecodedStruct { Name = "c1", StructType = "Chunk", Offset = 50, Size = 50, Children = [] },
            ],
        };

        DecodedNodeTreeBuilder.GetDisplayText(node).Should().Be("chunks [2 items]");
    }

    [Fact]
    public void GetDisplayText_String_ShowsQuotedValue()
    {
        var node = new DecodedString
        {
            Name = "sig",
            Offset = 0,
            Size = 3,
            Value = "PNG",
            Encoding = "ASCII",
        };

        DecodedNodeTreeBuilder.GetDisplayText(node).Should().Be("sig: \"PNG\"");
    }

    [Fact]
    public void GetDisplayText_Error_ShowsErrorWithMarker()
    {
        var node = new DecodedError
        {
            Name = "bad",
            Offset = 0,
            Size = 0,
            ErrorMessage = "unexpected EOF",
        };

        DecodedNodeTreeBuilder.GetDisplayText(node).Should().Be("\u2717 bad: unexpected EOF");
    }
}
