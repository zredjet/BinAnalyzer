using BinAnalyzer.Core.Expressions;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Expressions;

public class MemberAccessExpressionTests
{
    [Fact]
    public void DotAccess_ParsesFieldReference()
    {
        var expr = ExpressionParser.Parse("{parent.child}");
        var ma = expr.Root.Should().BeOfType<ExpressionNode.MemberAccess>().Subject;
        ma.Object.Should().BeOfType<ExpressionNode.FieldReference>()
            .Which.FieldName.Should().Be("parent");
        ma.MemberName.Should().Be("child");
    }

    [Fact]
    public void DotAccess_AfterIndexAccess()
    {
        var expr = ExpressionParser.Parse("{arr[0].field}");
        var ma = expr.Root.Should().BeOfType<ExpressionNode.MemberAccess>().Subject;
        ma.MemberName.Should().Be("field");
        var idx = ma.Object.Should().BeOfType<ExpressionNode.IndexAccess>().Subject;
        idx.ArrayName.Should().Be("arr");
        idx.Index.Should().BeOfType<ExpressionNode.LiteralInt>()
            .Which.Value.Should().Be(0);
    }

    [Fact]
    public void DotAccess_ChainedDots()
    {
        var expr = ExpressionParser.Parse("{a.b.c}");
        var outer = expr.Root.Should().BeOfType<ExpressionNode.MemberAccess>().Subject;
        outer.MemberName.Should().Be("c");
        var inner = outer.Object.Should().BeOfType<ExpressionNode.MemberAccess>().Subject;
        inner.MemberName.Should().Be("b");
        inner.Object.Should().BeOfType<ExpressionNode.FieldReference>()
            .Which.FieldName.Should().Be("a");
    }

    [Fact]
    public void DotAccess_IndexThenChainedDots()
    {
        var expr = ExpressionParser.Parse("{arr[i].x.y}");
        var outer = expr.Root.Should().BeOfType<ExpressionNode.MemberAccess>().Subject;
        outer.MemberName.Should().Be("y");
        var inner = outer.Object.Should().BeOfType<ExpressionNode.MemberAccess>().Subject;
        inner.MemberName.Should().Be("x");
        inner.Object.Should().BeOfType<ExpressionNode.IndexAccess>()
            .Which.ArrayName.Should().Be("arr");
    }

    [Fact]
    public void DotAccess_InArithmetic()
    {
        var expr = ExpressionParser.Parse("{arr[0].offset + 4}");
        var binOp = expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Add);
        binOp.Left.Should().BeOfType<ExpressionNode.MemberAccess>()
            .Which.MemberName.Should().Be("offset");
        binOp.Right.Should().BeOfType<ExpressionNode.LiteralInt>()
            .Which.Value.Should().Be(4);
    }

    [Fact]
    public void DotAccess_MissingMemberName_Throws()
    {
        var act = () => ExpressionParser.Parse("{parent.}");
        act.Should().Throw<FormatException>();
    }
}
