using BinAnalyzer.Core.Expressions;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Expressions;

public class BitwiseExpressionTests
{
    // --- Tokenizer tests ---

    [Fact]
    public void Tokenize_BitwiseOperators()
    {
        var tokens = ExpressionTokenizer.Tokenize("& | ^ << >>");
        tokens.Select(t => t.Type).Should().ContainInOrder(
            ExpressionTokenType.Ampersand,
            ExpressionTokenType.Pipe,
            ExpressionTokenType.Caret,
            ExpressionTokenType.LessLess,
            ExpressionTokenType.GreaterGreater);
    }

    [Fact]
    public void Tokenize_ShiftDoesNotConflictWithComparison()
    {
        var tokens = ExpressionTokenizer.Tokenize("<< <= < >> >= >");
        tokens.Select(t => t.Type).Should().ContainInOrder(
            ExpressionTokenType.LessLess,
            ExpressionTokenType.LessThanOrEqual,
            ExpressionTokenType.LessThan,
            ExpressionTokenType.GreaterGreater,
            ExpressionTokenType.GreaterThanOrEqual,
            ExpressionTokenType.GreaterThan);
    }

    // --- Parser tests ---

    [Fact]
    public void Parse_BitwiseAnd()
    {
        var expr = ExpressionParser.Parse("{a & 0xFF}");
        var binOp = expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.BitwiseAnd);
    }

    [Fact]
    public void Parse_BitwiseOr()
    {
        var expr = ExpressionParser.Parse("{a | 0x80}");
        var binOp = expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.BitwiseOr);
    }

    [Fact]
    public void Parse_BitwiseXor()
    {
        var expr = ExpressionParser.Parse("{a ^ b}");
        var binOp = expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.BitwiseXor);
    }

    [Fact]
    public void Parse_LeftShift()
    {
        var expr = ExpressionParser.Parse("{a << 4}");
        var binOp = expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.LeftShift);
    }

    [Fact]
    public void Parse_RightShift()
    {
        var expr = ExpressionParser.Parse("{a >> 4}");
        var binOp = expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.RightShift);
    }

    [Fact]
    public void Parse_Precedence_ShiftBeforeComparison()
    {
        // a << 4 > 0xFF should parse as (a << 4) > 0xFF
        var expr = ExpressionParser.Parse("{a << 4 > 0xFF}");
        var compare = expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        compare.Operator.Should().Be(BinaryOperator.GreaterThan);
        var shift = compare.Left.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        shift.Operator.Should().Be(BinaryOperator.LeftShift);
    }

    [Fact]
    public void Parse_Precedence_BitwiseAndBeforeXor()
    {
        // a & b ^ c should parse as (a & b) ^ c
        var expr = ExpressionParser.Parse("{a & b ^ c}");
        var xor = expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        xor.Operator.Should().Be(BinaryOperator.BitwiseXor);
        var and = xor.Left.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        and.Operator.Should().Be(BinaryOperator.BitwiseAnd);
    }

    [Fact]
    public void Parse_Precedence_BitwiseXorBeforeOr()
    {
        // a ^ b | c should parse as (a ^ b) | c
        var expr = ExpressionParser.Parse("{a ^ b | c}");
        var or = expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        or.Operator.Should().Be(BinaryOperator.BitwiseOr);
        var xor = or.Left.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        xor.Operator.Should().Be(BinaryOperator.BitwiseXor);
    }

    [Fact]
    public void Parse_Precedence_BitwiseOrBeforeLogicalAnd()
    {
        // a | b and c | d should parse as (a | b) and (c | d)
        var expr = ExpressionParser.Parse("{a | b and c | d}");
        var logicAnd = expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        logicAnd.Operator.Should().Be(BinaryOperator.And);
        logicAnd.Left.Should().BeOfType<ExpressionNode.BinaryOp>()
            .Which.Operator.Should().Be(BinaryOperator.BitwiseOr);
        logicAnd.Right.Should().BeOfType<ExpressionNode.BinaryOp>()
            .Which.Operator.Should().Be(BinaryOperator.BitwiseOr);
    }

    [Fact]
    public void Parse_Precedence_AddBeforeShift()
    {
        // a + 1 << 4 should parse as (a + 1) << 4
        var expr = ExpressionParser.Parse("{a + 1 << 4}");
        var shift = expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        shift.Operator.Should().Be(BinaryOperator.LeftShift);
        var add = shift.Left.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        add.Operator.Should().Be(BinaryOperator.Add);
    }
}
