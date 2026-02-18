using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Diff;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class DiffStatisticsTests
{
    [Fact]
    public void Compare_IdenticalStructs_AllUnchanged()
    {
        var left = CreateStruct("root", [
            CreateInteger("width", 100),
            CreateInteger("height", 200),
        ]);
        var right = CreateStruct("root", [
            CreateInteger("width", 100),
            CreateInteger("height", 200),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.Statistics.Should().NotBeNull();
        result.Statistics!.ChangedCount.Should().Be(0);
        result.Statistics.AddedCount.Should().Be(0);
        result.Statistics.RemovedCount.Should().Be(0);
        result.Statistics.UnchangedCount.Should().Be(2);
        result.Statistics.TotalFieldCount.Should().Be(2);
        result.Statistics.TotalDiffCount.Should().Be(0);
    }

    [Fact]
    public void Compare_ChangedFields_CountsCorrectly()
    {
        var left = CreateStruct("root", [
            CreateInteger("width", 100),
            CreateInteger("height", 200),
        ]);
        var right = CreateStruct("root", [
            CreateInteger("width", 999),
            CreateInteger("height", 200),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.Statistics!.ChangedCount.Should().Be(1);
        result.Statistics.UnchangedCount.Should().Be(1);
        result.Statistics.TotalFieldCount.Should().Be(2);
    }

    [Fact]
    public void Compare_AddedFields_CountsCorrectly()
    {
        var left = CreateStruct("root", [
            CreateInteger("width", 100),
        ]);
        var right = CreateStruct("root", [
            CreateInteger("width", 100),
            CreateInteger("height", 200),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.Statistics!.AddedCount.Should().Be(1);
        result.Statistics.UnchangedCount.Should().Be(1);
        result.Statistics.TotalFieldCount.Should().Be(2);
    }

    [Fact]
    public void Compare_RemovedFields_CountsCorrectly()
    {
        var left = CreateStruct("root", [
            CreateInteger("width", 100),
            CreateInteger("height", 200),
        ]);
        var right = CreateStruct("root", [
            CreateInteger("width", 100),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.Statistics!.RemovedCount.Should().Be(1);
        result.Statistics.UnchangedCount.Should().Be(1);
        result.Statistics.TotalFieldCount.Should().Be(2);
    }

    [Fact]
    public void Compare_MixedChanges_StatisticsCorrect()
    {
        var left = CreateStruct("root", [
            CreateInteger("a", 1),
            CreateInteger("b", 2),
            CreateInteger("c", 3),
        ]);
        var right = CreateStruct("root", [
            CreateInteger("a", 1),
            CreateInteger("b", 999),
            CreateInteger("d", 4),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.Statistics!.ChangedCount.Should().Be(1); // b changed
        result.Statistics.RemovedCount.Should().Be(1);  // c removed
        result.Statistics.AddedCount.Should().Be(1);    // d added
        result.Statistics.UnchangedCount.Should().Be(1); // a unchanged
        result.Statistics.TotalFieldCount.Should().Be(4);
        result.Statistics.TotalDiffCount.Should().Be(3);
    }

    [Fact]
    public void Compare_NestedStruct_CountsLeafFields()
    {
        var left = CreateStruct("root", [
            CreateStruct("header", [
                CreateInteger("width", 100),
                CreateInteger("height", 200),
            ]),
            CreateInteger("extra", 42),
        ]);
        var right = CreateStruct("root", [
            CreateStruct("header", [
                CreateInteger("width", 100),
                CreateInteger("height", 300),
            ]),
            CreateInteger("extra", 42),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.Statistics!.ChangedCount.Should().Be(1); // height changed
        result.Statistics.UnchangedCount.Should().Be(2); // width + extra unchanged
        result.Statistics.TotalFieldCount.Should().Be(3);
    }

    [Fact]
    public void Compare_ArrayDiff_CountsElements()
    {
        var left = CreateStruct("root", [
            CreateArray("items", [
                CreateInteger("item", 1),
                CreateInteger("item", 2),
                CreateInteger("item", 3),
            ]),
        ]);
        var right = CreateStruct("root", [
            CreateArray("items", [
                CreateInteger("item", 1),
                CreateInteger("item", 99),
            ]),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.Statistics!.ChangedCount.Should().Be(1);  // item[1] changed
        result.Statistics.RemovedCount.Should().Be(1);   // item[2] removed
        result.Statistics.UnchangedCount.Should().Be(1); // item[0] unchanged
        result.Statistics.TotalFieldCount.Should().Be(3);
    }

    [Fact]
    public void MatchRate_AllIdentical_Returns1()
    {
        var left = CreateStruct("root", [
            CreateInteger("a", 1),
            CreateInteger("b", 2),
        ]);
        var right = CreateStruct("root", [
            CreateInteger("a", 1),
            CreateInteger("b", 2),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.Statistics!.MatchRate.Should().Be(1.0);
    }

    [Fact]
    public void MatchRate_NoFields_Returns1()
    {
        var stats = new DiffStatistics(0, 0, 0, 0);

        stats.MatchRate.Should().Be(1.0);
    }

    private static DecodedStruct CreateStruct(string name, List<DecodedNode> children)
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

    private static DecodedInteger CreateInteger(string name, long value)
    {
        return new DecodedInteger { Name = name, Offset = 0, Size = 4, Value = value };
    }

    private static DecodedArray CreateArray(string name, List<DecodedNode> elements)
    {
        return new DecodedArray { Name = name, Offset = 0, Size = 0, Elements = elements };
    }
}
