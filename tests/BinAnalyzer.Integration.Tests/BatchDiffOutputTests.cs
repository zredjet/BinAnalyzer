using BinAnalyzer.Core.Diff;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class BatchDiffOutputTests
{
    private readonly BatchDiffSummaryFormatter _formatter = new(ColorMode.Never);

    [Fact]
    public void BatchSummary_AllIdentical_ShowsAllIdenticalMessage()
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

        var output = _formatter.Format(result);

        output.Should().Contain("2 ファイル比較");
        output.Should().Contain("2 同一");
        output.Should().Contain("0 差分あり");
        output.Should().Contain("同一");
        output.Should().NotContain("エラー");
    }

    [Fact]
    public void BatchSummary_WithDifferences_ShowsDiffCounts()
    {
        var result = new BatchDiffResult
        {
            FileEntries = new[]
            {
                new BatchDiffFileEntry
                {
                    FileName = "file1.bin",
                    HasDifferences = true,
                    Statistics = new DiffStatistics(3, 1, 0, 5),
                },
            },
            LeftOnlyFiles = Array.Empty<string>(),
            RightOnlyFiles = Array.Empty<string>(),
        };

        var output = _formatter.Format(result);

        output.Should().Contain("差分あり");
        output.Should().Contain("1 差分あり");
        output.Should().Contain("file1.bin");
    }

    [Fact]
    public void BatchSummary_LeftOnlyFiles_ShowsList()
    {
        var result = new BatchDiffResult
        {
            FileEntries = Array.Empty<BatchDiffFileEntry>(),
            LeftOnlyFiles = new[] { "left_only.bin" },
            RightOnlyFiles = Array.Empty<string>(),
        };

        var output = _formatter.Format(result);

        output.Should().Contain("左ディレクトリのみ (1 件):");
        output.Should().Contain("- left_only.bin");
    }

    [Fact]
    public void BatchSummary_RightOnlyFiles_ShowsList()
    {
        var result = new BatchDiffResult
        {
            FileEntries = Array.Empty<BatchDiffFileEntry>(),
            LeftOnlyFiles = Array.Empty<string>(),
            RightOnlyFiles = new[] { "right_only.bin" },
        };

        var output = _formatter.Format(result);

        output.Should().Contain("右ディレクトリのみ (1 件):");
        output.Should().Contain("+ right_only.bin");
    }

    [Fact]
    public void BatchSummary_ErrorFile_ShowsErrorStatus()
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

        var output = _formatter.Format(result);

        output.Should().Contain("エラー");
        output.Should().Contain("broken.bin");
        output.Should().Contain("decode failed");
        output.Should().Contain("1 エラー");
    }

    [Fact]
    public void BatchSummary_MixedResults_ShowsCorrectTotals()
    {
        var result = new BatchDiffResult
        {
            FileEntries = new[]
            {
                new BatchDiffFileEntry
                {
                    FileName = "same.bin",
                    HasDifferences = false,
                    Statistics = new DiffStatistics(0, 0, 0, 5),
                },
                new BatchDiffFileEntry
                {
                    FileName = "changed.bin",
                    HasDifferences = true,
                    Statistics = new DiffStatistics(2, 0, 1, 5),
                },
                new BatchDiffFileEntry
                {
                    FileName = "broken.bin",
                    HasError = true,
                    ErrorMessage = "decode failed",
                },
            },
            LeftOnlyFiles = new[] { "left.bin" },
            RightOnlyFiles = new[] { "right.bin" },
        };

        var output = _formatter.Format(result);

        output.Should().Contain("3 ファイル比較");
        output.Should().Contain("1 同一");
        output.Should().Contain("1 差分あり");
        output.Should().Contain("1 エラー");
        output.Should().Contain("左ディレクトリのみ (1 件):");
        output.Should().Contain("右ディレクトリのみ (1 件):");
    }

    [Fact]
    public void BatchSummary_EmptyDirectories_HandlesGracefully()
    {
        var result = new BatchDiffResult
        {
            FileEntries = Array.Empty<BatchDiffFileEntry>(),
            LeftOnlyFiles = Array.Empty<string>(),
            RightOnlyFiles = Array.Empty<string>(),
        };

        var output = _formatter.Format(result);

        output.Should().Contain("バッチdiffレポート");
        output.Should().Contain("0 ファイル比較");
        output.Should().Contain("0 同一");
        output.Should().Contain("0 差分あり");
        output.Should().NotContain("ファイル比較結果:");
        output.Should().NotContain("左ディレクトリのみ");
        output.Should().NotContain("右ディレクトリのみ");
    }
}
