using BinAnalyzer.Core.Expressions;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Expressions;

public class IndexAccessExpressionTests
{
    [Fact]
    public void IndexAccess_Parse_LiteralIndex()
    {
        var expr = ExpressionParser.Parse("{arr[0]}");
        var idx = expr.Root.Should().BeOfType<ExpressionNode.IndexAccess>().Subject;
        idx.ArrayName.Should().Be("arr");
        idx.Index.Should().BeOfType<ExpressionNode.LiteralInt>()
            .Which.Value.Should().Be(0);
    }

    [Fact]
    public void IndexAccess_Parse_FieldRefIndex()
    {
        var expr = ExpressionParser.Parse("{arr[_index]}");
        var idx = expr.Root.Should().BeOfType<ExpressionNode.IndexAccess>().Subject;
        idx.ArrayName.Should().Be("arr");
        idx.Index.Should().BeOfType<ExpressionNode.FieldReference>()
            .Which.FieldName.Should().Be("_index");
    }

    [Fact]
    public void IndexAccess_Parse_ExpressionIndex()
    {
        var expr = ExpressionParser.Parse("{arr[_index + 1]}");
        var idx = expr.Root.Should().BeOfType<ExpressionNode.IndexAccess>().Subject;
        idx.ArrayName.Should().Be("arr");
        var binOp = idx.Index.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Add);
        binOp.Left.Should().BeOfType<ExpressionNode.FieldReference>()
            .Which.FieldName.Should().Be("_index");
        binOp.Right.Should().BeOfType<ExpressionNode.LiteralInt>()
            .Which.Value.Should().Be(1);
    }

    [Fact]
    public void IndexAccess_Parse_InArithmetic()
    {
        var expr = ExpressionParser.Parse("{arr[0] + 10}");
        var binOp = expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Add);
        binOp.Left.Should().BeOfType<ExpressionNode.IndexAccess>();
        binOp.Right.Should().BeOfType<ExpressionNode.LiteralInt>()
            .Which.Value.Should().Be(10);
    }

    [Fact]
    public void IndexAccess_Parse_UnmatchedBracket_Throws()
    {
        var act = () => ExpressionParser.Parse("{arr[0}");
        act.Should().Throw<FormatException>();
    }
}
