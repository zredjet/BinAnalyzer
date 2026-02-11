using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class ParseIntFunctionTests
{
    private static DecodeContext CreateContextWithField(string fieldName, string value)
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable(fieldName, value);
        return ctx;
    }

    [Fact]
    public void ParseInt_OctalString_ReturnsCorrectValue()
    {
        var ctx = CreateContextWithField("size", "0000644\0");
        var expr = ExpressionParser.Parse("{parse_int(size, 8)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(420);
    }

    [Fact]
    public void ParseInt_DecimalString_ReturnsCorrectValue()
    {
        var ctx = CreateContextWithField("size", "12345");
        var expr = ExpressionParser.Parse("{parse_int(size, 10)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(12345);
    }

    [Fact]
    public void ParseInt_HexString_ReturnsCorrectValue()
    {
        var ctx = CreateContextWithField("size", "1A2B");
        var expr = ExpressionParser.Parse("{parse_int(size, 16)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(6699);
    }

    [Fact]
    public void ParseInt_NullTerminated_TrimsCorrectly()
    {
        var ctx = CreateContextWithField("size", "0000644\0\0\0\0\0");
        var expr = ExpressionParser.Parse("{parse_int(size, 8)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(420);
    }

    [Fact]
    public void ParseInt_SpacePadded_TrimsCorrectly()
    {
        var ctx = CreateContextWithField("size", "644     ");
        var expr = ExpressionParser.Parse("{parse_int(size, 8)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(420);
    }

    [Fact]
    public void ParseInt_EmptyAfterTrim_ReturnsZero()
    {
        var ctx = CreateContextWithField("size", "\0\0\0\0");
        var expr = ExpressionParser.Parse("{parse_int(size, 8)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(0);
    }

    [Fact]
    public void ParseInt_InvalidString_ReturnsZero()
    {
        var ctx = CreateContextWithField("size", "notanumber");
        var expr = ExpressionParser.Parse("{parse_int(size, 10)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(0);
    }

    [Fact]
    public void ParseInt_WrongArgCount_Throws()
    {
        var ctx = CreateContextWithField("size", "123");
        var expr = ExpressionParser.Parse("{parse_int(size)}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*2 arguments*");
    }

    [Fact]
    public void ParseInt_InvalidBase_Throws()
    {
        var ctx = CreateContextWithField("size", "123");
        var expr = ExpressionParser.Parse("{parse_int(size, 7)}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*base must be 2, 8, 10, or 16*");
    }

    [Fact]
    public void ParseInt_ZeroValue_ReturnsZero()
    {
        var ctx = CreateContextWithField("size", "00000000000\0");
        var expr = ExpressionParser.Parse("{parse_int(size, 8)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(0);
    }

    [Fact]
    public void ParseInt_InArithmeticExpression_Works()
    {
        var ctx = CreateContextWithField("size", "00000001000\0");
        // parse_int("00000001000\0", 8) = 512
        // ((512 + 511) / 512) * 512 = 512
        var expr = ExpressionParser.Parse("{((parse_int(size, 8) + 511) / 512) * 512}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(512);
    }

    [Fact]
    public void ParseInt_BinaryString_ReturnsCorrectValue()
    {
        var ctx = CreateContextWithField("size", "1010");
        var expr = ExpressionParser.Parse("{parse_int(size, 2)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(10);
    }
}
