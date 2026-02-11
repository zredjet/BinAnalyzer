using BinAnalyzer.Core.Expressions;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests;

public class TernaryExpressionTests
{
    [Fact]
    public void Ternary_Parse_TrueCondition()
    {
        var expr = ExpressionParser.Parse("{1 == 1 ? 'yes' : 'no'}");
        expr.Root.Should().BeOfType<ExpressionNode.Conditional>();
    }

    [Fact]
    public void Ternary_Parse_FalseCondition()
    {
        var expr = ExpressionParser.Parse("{1 == 2 ? 'yes' : 'no'}");
        expr.Root.Should().BeOfType<ExpressionNode.Conditional>();
        var cond = (ExpressionNode.Conditional)expr.Root;
        cond.TrueExpr.Should().BeOfType<ExpressionNode.LiteralString>();
        cond.FalseExpr.Should().BeOfType<ExpressionNode.LiteralString>();
    }

    [Fact]
    public void Ternary_NestedTernary_RightAssociative()
    {
        // a == 1 ? 'one' : a == 2 ? 'two' : 'other'
        // should parse as: a == 1 ? 'one' : (a == 2 ? 'two' : 'other')
        var expr = ExpressionParser.Parse("{a == 1 ? 'one' : a == 2 ? 'two' : 'other'}");
        var cond = expr.Root.Should().BeOfType<ExpressionNode.Conditional>().Subject;
        cond.FalseExpr.Should().BeOfType<ExpressionNode.Conditional>();
    }

    [Fact]
    public void Ternary_InParentheses()
    {
        // (flag == 1 ? 10 : 20) + 5
        var expr = ExpressionParser.Parse("{(flag == 1 ? 10 : 20) + 5}");
        expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>();
    }

    [Fact]
    public void Ternary_MissingColon_Throws()
    {
        var act = () => ExpressionParser.Parse("{1 == 1 ? 'yes'}");
        act.Should().Throw<FormatException>().WithMessage("*':'*");
    }

    [Fact]
    public void Ternary_IntegerResult()
    {
        var expr = ExpressionParser.Parse("{flag > 0 ? 100 : 0}");
        var cond = expr.Root.Should().BeOfType<ExpressionNode.Conditional>().Subject;
        cond.TrueExpr.Should().BeOfType<ExpressionNode.LiteralInt>();
        cond.FalseExpr.Should().BeOfType<ExpressionNode.LiteralInt>();
    }

    [Fact]
    public void Ternary_WithFieldReference()
    {
        var expr = ExpressionParser.Parse("{type == 'II' ? 'little' : 'big'}");
        var cond = expr.Root.Should().BeOfType<ExpressionNode.Conditional>().Subject;
        cond.Condition.Should().BeOfType<ExpressionNode.BinaryOp>();
    }
}
