using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Tui;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Tui.Tests;

public sealed class TuiStateTests
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
    public void Search_SetsResultsAndIndex()
    {
        var state = new TuiState();
        var root = CreateTestTree();

        state.Search(root, "width");

        state.SearchResults.Should().HaveCount(2);
        state.SearchIndex.Should().Be(0);
    }

    [Fact]
    public void Search_EmptyQuery_ClearsResults()
    {
        var state = new TuiState();
        var root = CreateTestTree();

        state.Search(root, "width");
        state.Search(root, "");

        state.SearchResults.Should().BeEmpty();
        state.SearchIndex.Should().Be(-1);
    }

    [Fact]
    public void NextSearchResult_CyclesThrough()
    {
        var state = new TuiState();
        var root = CreateTestTree();
        state.Search(root, "width");

        var first = state.SearchResults[0];
        var second = state.SearchResults[1];

        state.NextSearchResult().Should().Be(second); // index 0 -> 1
        state.NextSearchResult().Should().Be(first);  // index 1 -> 0 (wrap)
    }

    [Fact]
    public void PreviousSearchResult_CyclesBackward()
    {
        var state = new TuiState();
        var root = CreateTestTree();
        state.Search(root, "width");

        var first = state.SearchResults[0];
        var second = state.SearchResults[1];

        state.PreviousSearchResult().Should().Be(second); // index 0 -> 1 (wrap backward)
        state.PreviousSearchResult().Should().Be(first);   // index 1 -> 0
    }

    [Fact]
    public void NextSearchResult_NoResults_ReturnsNull()
    {
        var state = new TuiState();

        state.NextSearchResult().Should().BeNull();
    }

    [Fact]
    public void SelectedNodeChanged_FiresOnChange()
    {
        var state = new TuiState();
        var fired = false;
        state.SelectedNodeChanged += (_, _) => fired = true;

        state.SelectedNode = new DecodedInteger { Name = "test", Offset = 0, Size = 4, Value = 1 };

        fired.Should().BeTrue();
    }

    [Fact]
    public void SelectedNodeChanged_DoesNotFireOnSameValue()
    {
        var node = new DecodedInteger { Name = "test", Offset = 0, Size = 4, Value = 1 };
        var state = new TuiState { SelectedNode = node };

        var fired = false;
        state.SelectedNodeChanged += (_, _) => fired = true;

        state.SelectedNode = node;

        fired.Should().BeFalse();
    }

    [Fact]
    public void Search_FiresSearchResultsChanged()
    {
        var state = new TuiState();
        var root = CreateTestTree();
        var fired = false;
        state.SearchResultsChanged += (_, _) => fired = true;

        state.Search(root, "width");

        fired.Should().BeTrue();
    }
}
