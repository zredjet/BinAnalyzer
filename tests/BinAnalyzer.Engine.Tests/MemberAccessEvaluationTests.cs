using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class MemberAccessEvaluationTests
{
    [Fact]
    public void MemberAccess_ResolvesStructField()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("parent", new Dictionary<string, object>
        {
            ["child"] = 42L,
        });

        var expr = ExpressionParser.Parse("{parent.child}");
        var result = ExpressionEvaluator.EvaluateAsLong(expr, ctx);
        result.Should().Be(42);
    }

    [Fact]
    public void MemberAccess_IndexThenMember()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("arr", new List<object>
        {
            new Dictionary<string, object> { ["field"] = 100L },
            new Dictionary<string, object> { ["field"] = 200L },
        });

        var expr = ExpressionParser.Parse("{arr[1].field}");
        var result = ExpressionEvaluator.EvaluateAsLong(expr, ctx);
        result.Should().Be(200);
    }

    [Fact]
    public void MemberAccess_ChainedDots()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("a", new Dictionary<string, object>
        {
            ["b"] = new Dictionary<string, object>
            {
                ["c"] = 99L,
            },
        });

        var expr = ExpressionParser.Parse("{a.b.c}");
        var result = ExpressionEvaluator.EvaluateAsLong(expr, ctx);
        result.Should().Be(99);
    }

    [Fact]
    public void MemberAccess_NonDictThrows()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("parent", 42L);

        var expr = ExpressionParser.Parse("{parent.child}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a struct*");
    }

    [Fact]
    public void MemberAccess_MissingMemberThrows()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("parent", new Dictionary<string, object>
        {
            ["child"] = 42L,
        });

        var expr = ExpressionParser.Parse("{parent.missing}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found in struct*");
    }

    [Fact]
    public void MemberAccess_IndexOutOfRangeThrows()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("arr", new List<object>
        {
            new Dictionary<string, object> { ["field"] = 100L },
        });

        var expr = ExpressionParser.Parse("{arr[99].field}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*out of range*");
    }

    [Fact]
    public void MemberAccess_WithArithmetic()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("arr", new List<object>
        {
            new Dictionary<string, object> { ["offset"] = 10L },
            new Dictionary<string, object> { ["offset"] = 30L },
        });

        var expr = ExpressionParser.Parse("{arr[0].offset + arr[1].offset}");
        var result = ExpressionEvaluator.EvaluateAsLong(expr, ctx);
        result.Should().Be(40);
    }
}
