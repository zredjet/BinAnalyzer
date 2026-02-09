using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class ExpressionEvaluatorTests
{
    private static DecodeContext CreateContext()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("length", 13L);
        ctx.SetVariable("type", "IHDR");
        ctx.SetVariable("count", 5L);
        return ctx;
    }

    [Fact]
    public void Evaluate_FieldReference()
    {
        var ctx = CreateContext();
        var expr = ExpressionParser.Parse("{length}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(13);
    }

    [Fact]
    public void Evaluate_Subtraction()
    {
        var ctx = CreateContext();
        var expr = ExpressionParser.Parse("{length - 4}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(9);
    }

    [Fact]
    public void Evaluate_StringEquality()
    {
        var ctx = CreateContext();
        var expr = ExpressionParser.Parse("{type == 'IHDR'}");
        var result = ExpressionEvaluator.Evaluate(expr, ctx);
        result.Should().Be(true);
    }

    [Fact]
    public void Evaluate_StringInequality()
    {
        var ctx = CreateContext();
        var expr = ExpressionParser.Parse("{type == 'tEXt'}");
        var result = ExpressionEvaluator.Evaluate(expr, ctx);
        result.Should().Be(false);
    }

    [Fact]
    public void Evaluate_Multiplication()
    {
        var ctx = CreateContext();
        var expr = ExpressionParser.Parse("{count * 2}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(10);
    }

    [Fact]
    public void Evaluate_LiteralInt()
    {
        var ctx = CreateContext();
        var expr = ExpressionParser.Parse("{42}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(42);
    }

    [Fact]
    public void Evaluate_Comparison()
    {
        var ctx = CreateContext();
        var expr = ExpressionParser.Parse("{length > 10}");
        ExpressionEvaluator.EvaluateAsBool(expr, ctx).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_MissingVariable_Throws()
    {
        var ctx = CreateContext();
        var expr = ExpressionParser.Parse("{missing}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>().WithMessage("*missing*");
    }

    [Fact]
    public void Evaluate_Remaining_ReturnsCurrentScopeRemaining()
    {
        // 10バイトデータ、1バイト読み進めた後 → remaining = 9
        var data = new byte[10];
        var ctx = new DecodeContext(data, Endianness.Big);
        ctx.ReadUInt8(); // position = 1

        var expr = ExpressionParser.Parse("{remaining}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(9);
    }

    [Fact]
    public void Evaluate_RemainingSubtract_ReturnsCorrectValue()
    {
        var data = new byte[20];
        var ctx = new DecodeContext(data, Endianness.Big);

        var expr = ExpressionParser.Parse("{remaining - 8}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(12);
    }

    [Fact]
    public void Evaluate_RemainingComparison_Works()
    {
        var data = new byte[10];
        var ctx = new DecodeContext(data, Endianness.Big);

        var expr = ExpressionParser.Parse("{remaining > 0}");
        ExpressionEvaluator.EvaluateAsBool(expr, ctx).Should().BeTrue();
    }
}
