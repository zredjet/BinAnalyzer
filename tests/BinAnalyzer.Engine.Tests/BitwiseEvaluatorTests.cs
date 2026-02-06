using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class BitwiseEvaluatorTests
{
    private static DecodeContext CreateContext()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("flags", 0xABL);
        ctx.SetVariable("mask", 0x0FL);
        ctx.SetVariable("value", 1L);
        return ctx;
    }

    [Fact]
    public void Evaluate_BitwiseAnd()
    {
        var ctx = CreateContext();
        var expr = ExpressionParser.Parse("{flags & mask}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(0x0B);
    }

    [Fact]
    public void Evaluate_BitwiseOr()
    {
        var ctx = CreateContext();
        var expr = ExpressionParser.Parse("{flags | 0x100}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(0x1AB);
    }

    [Fact]
    public void Evaluate_BitwiseXor()
    {
        var ctx = CreateContext();
        var expr = ExpressionParser.Parse("{flags ^ 0xFF}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(0x54);
    }

    [Fact]
    public void Evaluate_LeftShift()
    {
        var ctx = CreateContext();
        var expr = ExpressionParser.Parse("{value << 8}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(256);
    }

    [Fact]
    public void Evaluate_RightShift()
    {
        var ctx = CreateContext();
        var expr = ExpressionParser.Parse("{flags >> 4}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(0x0A);
    }

    [Fact]
    public void Evaluate_CombinedBitwise_MaskAndShift()
    {
        var ctx = CreateContext();
        // (flags >> 4) & 0x0F = 0x0A
        var expr = ExpressionParser.Parse("{(flags >> 4) & 0x0F}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(0x0A);
    }

    [Fact]
    public void Evaluate_BitwiseOr_CombineFields()
    {
        var ctx = CreateContext();
        // (value << 8) | flags = 0x1AB
        var expr = ExpressionParser.Parse("{(value << 8) | flags}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(0x1AB);
    }
}
