using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Presentation;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Presentation.Tests;

public sealed class NodeChildrenTests
{
    [Fact]
    public void HasChildren_Struct_WithChildren_ReturnsTrue()
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

        NodeChildren.HasChildren(node).Should().BeTrue();
    }

    [Fact]
    public void HasChildren_Struct_Empty_ReturnsFalse()
    {
        var node = new DecodedStruct
        {
            Name = "empty",
            StructType = "Empty",
            Offset = 0,
            Size = 0,
            Children = [],
        };

        NodeChildren.HasChildren(node).Should().BeFalse();
    }

    [Fact]
    public void HasChildren_Struct_OnlyPadding_ReturnsFalse()
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

        NodeChildren.HasChildren(node).Should().BeFalse();
    }

    [Fact]
    public void HasChildren_Array_WithElements_ReturnsTrue()
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

        NodeChildren.HasChildren(node).Should().BeTrue();
    }

    [Fact]
    public void HasChildren_Integer_ReturnsFalse()
    {
        var node = new DecodedInteger
        {
            Name = "value",
            Offset = 0,
            Size = 4,
            Value = 42,
        };

        NodeChildren.HasChildren(node).Should().BeFalse();
    }

    [Fact]
    public void Of_Struct_ReturnNonPaddingChildren()
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

        var children = NodeChildren.Of(node).ToList();

        children.Should().HaveCount(2);
        children.Should().Contain(child1);
        children.Should().Contain(child2);
        children.Should().NotContain(padding);
    }

    [Fact]
    public void Of_Array_ReturnsAllElements()
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

        var children = NodeChildren.Of(node).ToList();

        children.Should().HaveCount(2);
        children.Should().ContainInOrder(elem1, elem2);
    }

    [Fact]
    public void Of_Compressed_WithDecodedContent_ReturnsContentChildren()
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

        var children = NodeChildren.Of(node).ToList();

        children.Should().HaveCount(1);
        children.Should().Contain(innerChild);
    }

    [Fact]
    public void Of_Integer_ReturnsEmpty()
    {
        var node = new DecodedInteger
        {
            Name = "value",
            Offset = 0,
            Size = 4,
            Value = 42,
        };

        NodeChildren.Of(node).Should().BeEmpty();
    }

    [Fact]
    public void HasChildren_Bitfield_ReturnsFalse()
    {
        var node = new DecodedBitfield
        {
            Name = "bf", Offset = 0, Size = 1, RawValue = 0x3,
            Fields = [new BitfieldValue("lo", 0, 0, 1, null, null)],
        };

        NodeChildren.HasChildren(node).Should().BeFalse();
        NodeChildren.Of(node).Should().BeEmpty();
    }

    [Fact]
    public void Descendants_ReturnsPreOrder_WithoutPadding()
    {
        var a = new DecodedInteger { Name = "a", Offset = 0, Size = 1, Value = 1 };
        var pad = new DecodedBytes { Name = "pad", Offset = 1, Size = 1, RawBytes = new byte[1], IsPadding = true };
        var b = new DecodedInteger { Name = "b", Offset = 2, Size = 1, Value = 2 };
        var inner = new DecodedStruct { Name = "inner", StructType = "Inner", Offset = 2, Size = 1, Children = [b] };
        var root = new DecodedStruct { Name = "root", StructType = "Root", Offset = 0, Size = 3, Children = [a, pad, inner] };

        NodeChildren.Descendants(root).Select(n => n.Name).Should().Equal("root", "a", "inner", "b");
    }
}
