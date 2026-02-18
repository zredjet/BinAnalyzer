using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Diff;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class DiffSummaryOutputTests
{
    [Fact]
    public void Summary_NoDifferences_ShowsZeroCounts()
    {
        var left = MakeStruct("root", [
            MakeInteger("width", 100),
            MakeInteger("height", 200),
        ]);
        var right = MakeStruct("root", [
            MakeInteger("width", 100),
            MakeInteger("height", 200),
        ]);

        var result = DiffEngine.Compare(left, right);
        var formatter = new DiffSummaryFormatter();
        var output = formatter.Format(result.Statistics!);

        output.Should().Contain("統計");
        output.Should().Contain("差分なし");
        output.Should().Contain("一致: 2 / 2 フィールド (100.0%)");
    }

    [Fact]
    public void Summary_WithDifferences_ShowsCorrectCounts()
    {
        var left = MakeStruct("root", [
            MakeInteger("a", 1),
            MakeInteger("b", 2),
            MakeInteger("c", 3),
        ]);
        var right = MakeStruct("root", [
            MakeInteger("a", 1),
            MakeInteger("b", 999),
            MakeInteger("d", 4),
        ]);

        var result = DiffEngine.Compare(left, right);
        var formatter = new DiffSummaryFormatter();
        var output = formatter.Format(result.Statistics!);

        output.Should().Contain("変更: 1 件");
        output.Should().Contain("追加: 1 件");
        output.Should().Contain("削除: 1 件");
        output.Should().Contain("合計差分: 3 件");
    }

    [Fact]
    public void Summary_ShowsMatchRate()
    {
        var left = MakeStruct("root", [
            MakeInteger("a", 1),
            MakeInteger("b", 2),
            MakeInteger("c", 3),
            MakeInteger("d", 4),
        ]);
        var right = MakeStruct("root", [
            MakeInteger("a", 1),
            MakeInteger("b", 999),
            MakeInteger("c", 3),
            MakeInteger("d", 4),
        ]);

        var result = DiffEngine.Compare(left, right);
        var formatter = new DiffSummaryFormatter();
        var output = formatter.Format(result.Statistics!);

        // 3/4 unchanged = 75.0%
        output.Should().Contain("一致: 3 / 4 フィールド (75.0%)");
    }

    [Fact]
    public void SummaryOnly_OmitsDetailedDiff()
    {
        var left = MakeStruct("root", [
            MakeInteger("width", 100),
        ]);
        var right = MakeStruct("root", [
            MakeInteger("width", 200),
        ]);

        var result = DiffEngine.Compare(left, right);
        var summaryFormatter = new DiffSummaryFormatter();
        var summaryOutput = summaryFormatter.Format(result.Statistics!);

        // Summary should contain statistics but not detailed diff arrows
        summaryOutput.Should().Contain("統計");
        summaryOutput.Should().Contain("変更: 1 件");
        // Should not contain the detailed diff format "~ width: 100 → 200"
        summaryOutput.Should().NotContain("~");
        summaryOutput.Should().NotContain("→");
    }

    [Fact]
    public void SummaryWithDetails_ShowsBothOutputs()
    {
        var left = MakeStruct("root", [
            MakeInteger("width", 100),
        ]);
        var right = MakeStruct("root", [
            MakeInteger("width", 200),
        ]);

        var result = DiffEngine.Compare(left, right);

        // Simulate --summary mode: detailed diff + summary
        var diffFormatter = new DiffOutputFormatter();
        var summaryFormatter = new DiffSummaryFormatter();
        var detailOutput = diffFormatter.Format(result);
        var summaryOutput = summaryFormatter.Format(result.Statistics!);
        var combinedOutput = detailOutput + summaryOutput;

        // Should contain both detail and summary
        combinedOutput.Should().Contain("width");
        combinedOutput.Should().Contain("→");
        combinedOutput.Should().Contain("統計");
        combinedOutput.Should().Contain("変更: 1 件");
    }

    [Fact]
    public void ExistingBehavior_NoSummaryFlag_Unchanged()
    {
        var result = new DiffResult
        {
            Entries = [
                new DiffEntry(DiffKind.Changed, "header.width", "100", "200"),
            ],
        };
        var formatter = new DiffOutputFormatter();

        var output = formatter.Format(result);

        // Existing format should be unchanged - no summary section
        output.Should().Contain("header.width");
        output.Should().Contain("→");
        output.Should().NotContain("統計");
    }

    private static DecodedStruct MakeStruct(string name, List<DecodedNode> children)
    {
        return new DecodedStruct
        {
            Name = name,
            StructType = name,
            Offset = 0,
            Size = 0,
            Children = children,
        };
    }

    private static DecodedInteger MakeInteger(string name, long value)
    {
        return new DecodedInteger { Name = name, Offset = 0, Size = 4, Value = value };
    }
}
