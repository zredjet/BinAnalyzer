using BinAnalyzer.Core.Diff;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests;

public class BatchDiffResultTests
{
    [Fact]
    public void HasDifferences_AllIdentical_ReturnsFalse()
    {
        var result = new BatchDiffResult
        {
            FileEntries = new[]
            {
                new BatchDiffFileEntry
                {
                    FileName = "file1.bin",
                    HasDifferences = false,
                    Statistics = new DiffStatistics(0, 0, 0, 5),
                },
                new BatchDiffFileEntry
                {
                    FileName = "file2.bin",
                    HasDifferences = false,
                    Statistics = new DiffStatistics(0, 0, 0, 3),
                },
            },
            LeftOnlyFiles = Array.Empty<string>(),
            RightOnlyFiles = Array.Empty<string>(),
        };

        result.HasDifferences.Should().BeFalse();
    }

    [Fact]
    public void HasDifferences_FileDiffExists_ReturnsTrue()
    {
        var result = new BatchDiffResult
        {
            FileEntries = new[]
            {
                new BatchDiffFileEntry
                {
                    FileName = "file1.bin",
                    HasDifferences = true,
                    Statistics = new DiffStatistics(2, 1, 0, 5),
                },
            },
            LeftOnlyFiles = Array.Empty<string>(),
            RightOnlyFiles = Array.Empty<string>(),
        };

        result.HasDifferences.Should().BeTrue();
    }

    [Fact]
    public void HasDifferences_LeftOnlyExists_ReturnsTrue()
    {
        var result = new BatchDiffResult
        {
            FileEntries = Array.Empty<BatchDiffFileEntry>(),
            LeftOnlyFiles = new[] { "extra.bin" },
            RightOnlyFiles = Array.Empty<string>(),
        };

        result.HasDifferences.Should().BeTrue();
    }

    [Fact]
    public void HasDifferences_RightOnlyExists_ReturnsTrue()
    {
        var result = new BatchDiffResult
        {
            FileEntries = Array.Empty<BatchDiffFileEntry>(),
            LeftOnlyFiles = Array.Empty<string>(),
            RightOnlyFiles = new[] { "extra.bin" },
        };

        result.HasDifferences.Should().BeTrue();
    }

    [Fact]
    public void HasDifferences_ErrorExists_ReturnsTrue()
    {
        var result = new BatchDiffResult
        {
            FileEntries = new[]
            {
                new BatchDiffFileEntry
                {
                    FileName = "broken.bin",
                    HasError = true,
                    ErrorMessage = "decode failed",
                },
            },
            LeftOnlyFiles = Array.Empty<string>(),
            RightOnlyFiles = Array.Empty<string>(),
        };

        result.HasDifferences.Should().BeTrue();
    }
}
