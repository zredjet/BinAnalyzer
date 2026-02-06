using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests;

public class PathFilterTests
{
    [Fact]
    public void ExactMatch()
    {
        var filter = new PathFilter(["root.header.width"]);

        filter.Matches("root.header.width").Should().BeTrue();
        filter.Matches("root.header.height").Should().BeFalse();
        filter.Matches("root.header").Should().BeFalse();
    }

    [Fact]
    public void SingleWildcard_MatchesOneLevel()
    {
        var filter = new PathFilter(["root.chunks.*.type"]);

        filter.Matches("root.chunks.0.type").Should().BeTrue();
        filter.Matches("root.chunks.1.type").Should().BeTrue();
        filter.Matches("root.chunks.abc.type").Should().BeTrue();
        filter.Matches("root.chunks.type").Should().BeFalse();
        filter.Matches("root.chunks.0.1.type").Should().BeFalse();
    }

    [Fact]
    public void DoubleWildcard_MatchesZeroOrMoreLevels()
    {
        var filter = new PathFilter(["**.width"]);

        filter.Matches("root.width").Should().BeTrue();
        filter.Matches("root.header.width").Should().BeTrue();
        filter.Matches("root.a.b.c.width").Should().BeTrue();
        filter.Matches("width").Should().BeTrue();
        filter.Matches("root.height").Should().BeFalse();
    }

    [Fact]
    public void MultiplePatterns_OrCombination()
    {
        var filter = new PathFilter(["root.a", "root.b"]);

        filter.Matches("root.a").Should().BeTrue();
        filter.Matches("root.b").Should().BeTrue();
        filter.Matches("root.c").Should().BeFalse();
    }

    [Fact]
    public void IsAncestorOfMatch()
    {
        var filter = new PathFilter(["root.header.width"]);

        filter.IsAncestorOfMatch("root").Should().BeTrue();
        filter.IsAncestorOfMatch("root.header").Should().BeTrue();
        filter.IsAncestorOfMatch("root.header.width").Should().BeFalse(); // exact match, not ancestor
        filter.IsAncestorOfMatch("root.data").Should().BeFalse();
    }

    [Fact]
    public void ShouldInclude_MatchOrAncestor()
    {
        var filter = new PathFilter(["root.header.width"]);

        filter.ShouldInclude("root").Should().BeTrue();
        filter.ShouldInclude("root.header").Should().BeTrue();
        filter.ShouldInclude("root.header.width").Should().BeTrue();
        filter.ShouldInclude("root.data").Should().BeFalse();
    }

    [Fact]
    public void ArrayIndex_InPath()
    {
        var filter = new PathFilter(["root.chunks.0.type"]);

        filter.Matches("root.chunks.0.type").Should().BeTrue();
        filter.Matches("root.chunks.1.type").Should().BeFalse();
    }

    [Fact]
    public void WildcardAtEnd_MatchesAllChildren()
    {
        var filter = new PathFilter(["root.header.*"]);

        filter.Matches("root.header.width").Should().BeTrue();
        filter.Matches("root.header.height").Should().BeTrue();
        filter.Matches("root.header").Should().BeFalse();
        filter.Matches("root.header.sub.deep").Should().BeFalse();
    }

    [Fact]
    public void DoubleWildcard_AtStart()
    {
        var filter = new PathFilter(["**.type"]);

        filter.IsAncestorOfMatch("root").Should().BeTrue();
        filter.IsAncestorOfMatch("root.chunks").Should().BeTrue();
        filter.IsAncestorOfMatch("root.chunks.0").Should().BeTrue();
    }
}
