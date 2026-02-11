using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class ArrayIndexEvaluatorTests
{
    [Fact]
    public void IndexAccess_ReturnsElementValue()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("arr", new List<object> { 10L, 20L, 30L });

        var expr = ExpressionParser.Parse("{arr[1]}");
        var result = ExpressionEvaluator.EvaluateAsLong(expr, ctx);
        result.Should().Be(20);
    }

    [Fact]
    public void IndexAccess_WithFieldReferenceIndex()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("arr", new List<object> { 10L, 20L, 30L });
        ctx.SetVariable("_index", 2L);

        var expr = ExpressionParser.Parse("{arr[_index]}");
        var result = ExpressionEvaluator.EvaluateAsLong(expr, ctx);
        result.Should().Be(30);
    }

    [Fact]
    public void IndexAccess_OutOfRange_Throws()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("arr", new List<object> { 10L, 20L, 30L });

        var expr = ExpressionParser.Parse("{arr[5]}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*out of range*");
    }

    [Fact]
    public void IndexAccess_NotAnArray_Throws()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("arr", 42L);

        var expr = ExpressionParser.Parse("{arr[0]}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not an array*");
    }

    [Fact]
    public void IndexAccess_NegativeIndex_Throws()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("arr", new List<object> { 10L, 20L, 30L });

        var expr = ExpressionParser.Parse("{arr[-1]}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*out of range*");
    }
}
