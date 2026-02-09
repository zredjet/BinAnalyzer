using BinAnalyzer.Core.Expressions;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Expressions;

public class FunctionCallParserTests
{
    [Fact]
    public void Parse_FunctionCall_TwoArguments()
    {
        var expr = ExpressionParser.Parse("{until_marker(0xFF, 0xD9)}");
        var func = expr.Root.Should().BeOfType<ExpressionNode.FunctionCall>().Subject;
        func.Name.Should().Be("until_marker");
        func.Arguments.Should().HaveCount(2);
        func.Arguments[0].Should().BeOfType<ExpressionNode.LiteralInt>()
            .Which.Value.Should().Be(0xFF);
        func.Arguments[1].Should().BeOfType<ExpressionNode.LiteralInt>()
            .Which.Value.Should().Be(0xD9);
    }

    [Fact]
    public void Parse_FunctionCall_OneArgument()
    {
        var expr = ExpressionParser.Parse("{until_marker(0xFF)}");
        var func = expr.Root.Should().BeOfType<ExpressionNode.FunctionCall>().Subject;
        func.Name.Should().Be("until_marker");
        func.Arguments.Should().HaveCount(1);
        func.Arguments[0].Should().BeOfType<ExpressionNode.LiteralInt>()
            .Which.Value.Should().Be(0xFF);
    }

    [Fact]
    public void Parse_FunctionCall_NoArguments()
    {
        var expr = ExpressionParser.Parse("{until_marker()}");
        var func = expr.Root.Should().BeOfType<ExpressionNode.FunctionCall>().Subject;
        func.Name.Should().Be("until_marker");
        func.Arguments.Should().BeEmpty();
    }

    [Fact]
    public void Parse_IdentifierWithoutParens_RemainsFieldReference()
    {
        var expr = ExpressionParser.Parse("{length}");
        expr.Root.Should().BeOfType<ExpressionNode.FieldReference>()
            .Which.FieldName.Should().Be("length");
    }

    [Fact]
    public void Parse_FunctionCall_WithExpressionArguments()
    {
        var expr = ExpressionParser.Parse("{func(a + 1, b * 2)}");
        var func = expr.Root.Should().BeOfType<ExpressionNode.FunctionCall>().Subject;
        func.Name.Should().Be("func");
        func.Arguments.Should().HaveCount(2);
        func.Arguments[0].Should().BeOfType<ExpressionNode.BinaryOp>()
            .Which.Operator.Should().Be(BinaryOperator.Add);
        func.Arguments[1].Should().BeOfType<ExpressionNode.BinaryOp>()
            .Which.Operator.Should().Be(BinaryOperator.Multiply);
    }

    [Fact]
    public void Parse_FunctionCall_MissingClosingParen_Throws()
    {
        var act = () => ExpressionParser.Parse("{func(1, 2}");
        act.Should().Throw<FormatException>();
    }
}
