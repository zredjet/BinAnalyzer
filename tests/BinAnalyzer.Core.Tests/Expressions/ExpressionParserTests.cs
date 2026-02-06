using BinAnalyzer.Core.Expressions;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Expressions;

public class ExpressionParserTests
{
    [Fact]
    public void Parse_SimpleFieldReference()
    {
        var expr = ExpressionParser.Parse("{length}");
        expr.Root.Should().BeOfType<ExpressionNode.FieldReference>()
            .Which.FieldName.Should().Be("length");
        expr.OriginalText.Should().Be("{length}");
    }

    [Fact]
    public void Parse_IntegerLiteral()
    {
        var expr = ExpressionParser.Parse("{42}");
        expr.Root.Should().BeOfType<ExpressionNode.LiteralInt>()
            .Which.Value.Should().Be(42);
    }

    [Fact]
    public void Parse_HexLiteral()
    {
        var expr = ExpressionParser.Parse("{0xFF}");
        expr.Root.Should().BeOfType<ExpressionNode.LiteralInt>()
            .Which.Value.Should().Be(255);
    }

    [Fact]
    public void Parse_StringLiteral()
    {
        var expr = ExpressionParser.Parse("{'IHDR'}");
        expr.Root.Should().BeOfType<ExpressionNode.LiteralString>()
            .Which.Value.Should().Be("IHDR");
    }

    [Fact]
    public void Parse_Subtraction()
    {
        var expr = ExpressionParser.Parse("{length - 4}");
        var binOp = expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Subtract);
        binOp.Left.Should().BeOfType<ExpressionNode.FieldReference>()
            .Which.FieldName.Should().Be("length");
        binOp.Right.Should().BeOfType<ExpressionNode.LiteralInt>()
            .Which.Value.Should().Be(4);
    }

    [Fact]
    public void Parse_Equality()
    {
        var expr = ExpressionParser.Parse("{type == 'IHDR'}");
        var binOp = expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Equal);
        binOp.Left.Should().BeOfType<ExpressionNode.FieldReference>();
        binOp.Right.Should().BeOfType<ExpressionNode.LiteralString>();
    }

    [Fact]
    public void Parse_OperatorPrecedence_MulBeforeAdd()
    {
        // 2 + 3 * 4 should parse as 2 + (3 * 4)
        var expr = ExpressionParser.Parse("{2 + 3 * 4}");
        var add = expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        add.Operator.Should().Be(BinaryOperator.Add);
        add.Left.Should().BeOfType<ExpressionNode.LiteralInt>()
            .Which.Value.Should().Be(2);
        var mul = add.Right.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        mul.Operator.Should().Be(BinaryOperator.Multiply);
    }

    [Fact]
    public void Parse_Parentheses_OverridePrecedence()
    {
        // (2 + 3) * 4 should parse as (2 + 3) * 4
        var expr = ExpressionParser.Parse("{(2 + 3) * 4}");
        var mul = expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        mul.Operator.Should().Be(BinaryOperator.Multiply);
        var add = mul.Left.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        add.Operator.Should().Be(BinaryOperator.Add);
    }

    [Fact]
    public void Parse_UnaryNegate()
    {
        var expr = ExpressionParser.Parse("{-1}");
        var unary = expr.Root.Should().BeOfType<ExpressionNode.UnaryOp>().Subject;
        unary.Operator.Should().Be(UnaryOperator.Negate);
        unary.Operand.Should().BeOfType<ExpressionNode.LiteralInt>()
            .Which.Value.Should().Be(1);
    }

    [Fact]
    public void Parse_LogicalAndOr()
    {
        var expr = ExpressionParser.Parse("{a == 1 and b == 2 or c == 3}");
        // Should parse as (a==1 and b==2) or (c==3)
        var orOp = expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        orOp.Operator.Should().Be(BinaryOperator.Or);
        var andOp = orOp.Left.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        andOp.Operator.Should().Be(BinaryOperator.And);
    }

    [Fact]
    public void Parse_NotOperator()
    {
        var expr = ExpressionParser.Parse("{not done}");
        var unary = expr.Root.Should().BeOfType<ExpressionNode.UnaryOp>().Subject;
        unary.Operator.Should().Be(UnaryOperator.Not);
        unary.Operand.Should().BeOfType<ExpressionNode.FieldReference>()
            .Which.FieldName.Should().Be("done");
    }

    [Fact]
    public void Parse_WithoutBraces()
    {
        // Should also work without braces (for internal use)
        var expr = ExpressionParser.Parse("length");
        expr.Root.Should().BeOfType<ExpressionNode.FieldReference>()
            .Which.FieldName.Should().Be("length");
    }

    [Fact]
    public void Parse_UnexpectedToken_Throws()
    {
        var act = () => ExpressionParser.Parse("{+}");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_UnmatchedParen_Throws()
    {
        var act = () => ExpressionParser.Parse("{(a + b}");
        act.Should().Throw<FormatException>();
    }
}
