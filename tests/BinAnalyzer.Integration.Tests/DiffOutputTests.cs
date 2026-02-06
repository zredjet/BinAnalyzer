using BinAnalyzer.Core.Diff;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class DiffOutputTests
{
    [Fact]
    public void NoDifferences_DisplaysNoDiffMessage()
    {
        var result = new DiffResult { Entries = [] };
        var formatter = new DiffOutputFormatter();

        var output = formatter.Format(result);

        output.Should().Contain("差分なし");
    }

    [Fact]
    public void ChangedEntry_DisplaysArrow()
    {
        var result = new DiffResult
        {
            Entries = [
                new DiffEntry(DiffKind.Changed, "header.width", "100", "200"),
            ],
        };
        var formatter = new DiffOutputFormatter();

        var output = formatter.Format(result);

        output.Should().Contain("~");
        output.Should().Contain("header.width");
        output.Should().Contain("100");
        output.Should().Contain("→");
        output.Should().Contain("200");
    }

    [Fact]
    public void AddedEntry_DisplaysPlus()
    {
        var result = new DiffResult
        {
            Entries = [
                new DiffEntry(DiffKind.Added, "chunks[1]", null, "[struct]"),
            ],
        };
        var formatter = new DiffOutputFormatter();

        var output = formatter.Format(result);

        output.Should().Contain("+");
        output.Should().Contain("chunks[1]");
    }

    [Fact]
    public void RemovedEntry_DisplaysMinus()
    {
        var result = new DiffResult
        {
            Entries = [
                new DiffEntry(DiffKind.Removed, "chunks[2]", "[struct]", null),
            ],
        };
        var formatter = new DiffOutputFormatter();

        var output = formatter.Format(result);

        output.Should().Contain("-");
        output.Should().Contain("chunks[2]");
    }

    [Fact]
    public void MultipleEntries_DisplaysCount()
    {
        var result = new DiffResult
        {
            Entries = [
                new DiffEntry(DiffKind.Changed, "width", "100", "200"),
                new DiffEntry(DiffKind.Changed, "height", "100", "150"),
                new DiffEntry(DiffKind.Added, "extra", null, "new"),
            ],
        };
        var formatter = new DiffOutputFormatter();

        var output = formatter.Format(result);

        output.Should().Contain("差分: 3 件");
    }
}
