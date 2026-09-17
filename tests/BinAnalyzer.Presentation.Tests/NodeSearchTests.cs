using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Presentation;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Presentation.Tests;

public sealed class NodeSearchTests
{
    private static DecodedStruct CreateTestTree()
    {
        return new DecodedStruct
        {
            Name = "root",
            StructType = "Root",
            Offset = 0,
            Size = 100,
            Children =
            [
                new DecodedInteger { Name = "width", Offset = 0, Size = 4, Value = 800 },
                new DecodedInteger { Name = "height", Offset = 4, Size = 4, Value = 600 },
                new DecodedStruct
                {
                    Name = "nested",
                    StructType = "Nested",
                    Offset = 8,
                    Size = 10,
                    Children =
                    [
                        new DecodedInteger { Name = "width_inner", Offset = 8, Size = 4, Value = 100 },
                        new DecodedString { Name = "label", Offset = 12, Size = 6, Value = "test", Encoding = "UTF-8" },
                    ],
                },
            ],
        };
    }

    [Fact]
    public void ByName_FindsByName_CaseInsensitive()
    {
        var root = CreateTestTree();

        var results = NodeSearch.ByName(root, "WIDTH");

        results.Should().HaveCount(2);
        results.Select(r => r.Name).Should().Contain("width");
        results.Select(r => r.Name).Should().Contain("width_inner");
    }

    [Fact]
    public void ByName_NoMatch_ReturnsEmpty()
    {
        var root = CreateTestTree();

        var results = NodeSearch.ByName(root, "nonexistent");

        results.Should().BeEmpty();
    }

    [Fact]
    public void ByName_PartialMatch_FindsAll()
    {
        var root = CreateTestTree();

        var results = NodeSearch.ByName(root, "ght");

        results.Should().HaveCount(1);
        results[0].Name.Should().Be("height");
    }

    [Fact]
    public void ByName_InArray_FindsElements()
    {
        var root = new DecodedStruct
        {
            Name = "root",
            StructType = "Root",
            Offset = 0,
            Size = 100,
            Children =
            [
                new DecodedArray
                {
                    Name = "items",
                    Offset = 0,
                    Size = 100,
                    Elements =
                    [
                        new DecodedStruct
                        {
                            Name = "item0",
                            StructType = "Item",
                            Offset = 0,
                            Size = 50,
                            Children =
                            [
                                new DecodedInteger { Name = "target_field", Offset = 0, Size = 4, Value = 1 },
                            ],
                        },
                    ],
                },
            ],
        };

        var results = NodeSearch.ByName(root, "target");

        results.Should().HaveCount(1);
        results[0].Name.Should().Be("target_field");
    }

    [Fact]
    public void ByName_EmptyQuery_ReturnsEmpty()
    {
        NodeSearch.ByName(CreateTestTree(), "").Should().BeEmpty();
    }
}
