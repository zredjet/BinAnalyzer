using BinAnalyzer.Core.Decoded;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests;

public class NodeFilterHelperTests
{
    [Fact]
    public void FilterTree_KeepsMatchedLeafAndAncestors()
    {
        var root = new DecodedStruct
        {
            Name = "root",
            StructType = "root",
            Offset = 0,
            Size = 10,
            Children =
            [
                new DecodedInteger { Name = "a", Offset = 0, Size = 1, Value = 1 },
                new DecodedStruct
                {
                    Name = "header",
                    StructType = "header",
                    Offset = 1,
                    Size = 4,
                    Children =
                    [
                        new DecodedInteger { Name = "width", Offset = 1, Size = 2, Value = 100 },
                        new DecodedInteger { Name = "height", Offset = 3, Size = 2, Value = 200 },
                    ],
                },
            ],
        };

        var filter = new PathFilter(["root.header.width"]);
        var result = NodeFilterHelper.FilterTree(root, filter);

        result.Should().NotBeNull();
        result!.Children.Should().HaveCount(1);
        var header = result.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        header.Name.Should().Be("header");
        header.Children.Should().HaveCount(1);
        header.Children[0].Should().BeOfType<DecodedInteger>().Which.Name.Should().Be("width");
    }

    [Fact]
    public void FilterTree_NoMatch_ReturnsNull()
    {
        var root = new DecodedStruct
        {
            Name = "root",
            StructType = "root",
            Offset = 0,
            Size = 4,
            Children =
            [
                new DecodedInteger { Name = "a", Offset = 0, Size = 2, Value = 1 },
                new DecodedInteger { Name = "b", Offset = 2, Size = 2, Value = 2 },
            ],
        };

        var filter = new PathFilter(["root.nonexistent"]);
        var result = NodeFilterHelper.FilterTree(root, filter);

        result.Should().BeNull();
    }

    [Fact]
    public void FilterTree_ArrayFilter()
    {
        var root = new DecodedStruct
        {
            Name = "root",
            StructType = "root",
            Offset = 0,
            Size = 10,
            Children =
            [
                new DecodedArray
                {
                    Name = "items",
                    Offset = 0,
                    Size = 6,
                    Elements =
                    [
                        new DecodedStruct
                        {
                            Name = "items",
                            StructType = "item",
                            Offset = 0,
                            Size = 2,
                            Children =
                            [
                                new DecodedInteger { Name = "type", Offset = 0, Size = 1, Value = 1 },
                                new DecodedInteger { Name = "value", Offset = 1, Size = 1, Value = 10 },
                            ],
                        },
                        new DecodedStruct
                        {
                            Name = "items",
                            StructType = "item",
                            Offset = 2,
                            Size = 2,
                            Children =
                            [
                                new DecodedInteger { Name = "type", Offset = 2, Size = 1, Value = 2 },
                                new DecodedInteger { Name = "value", Offset = 3, Size = 1, Value = 20 },
                            ],
                        },
                    ],
                },
            ],
        };

        var filter = new PathFilter(["root.items.*.type"]);
        var result = NodeFilterHelper.FilterTree(root, filter);

        result.Should().NotBeNull();
        var array = result!.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);
        var elem0 = array.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        elem0.Children.Should().HaveCount(1);
        elem0.Children[0].Should().BeOfType<DecodedInteger>().Which.Name.Should().Be("type");
    }

    [Fact]
    public void FilterTree_MultiplePatterns()
    {
        var root = new DecodedStruct
        {
            Name = "root",
            StructType = "root",
            Offset = 0,
            Size = 6,
            Children =
            [
                new DecodedInteger { Name = "a", Offset = 0, Size = 2, Value = 1 },
                new DecodedInteger { Name = "b", Offset = 2, Size = 2, Value = 2 },
                new DecodedInteger { Name = "c", Offset = 4, Size = 2, Value = 3 },
            ],
        };

        var filter = new PathFilter(["root.a", "root.c"]);
        var result = NodeFilterHelper.FilterTree(root, filter);

        result.Should().NotBeNull();
        result!.Children.Should().HaveCount(2);
        result.Children[0].Should().BeOfType<DecodedInteger>().Which.Name.Should().Be("a");
        result.Children[1].Should().BeOfType<DecodedInteger>().Which.Name.Should().Be("c");
    }
}
