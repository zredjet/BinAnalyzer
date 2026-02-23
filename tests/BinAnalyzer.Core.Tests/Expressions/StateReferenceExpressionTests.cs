using BinAnalyzer.Core.Expressions;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Expressions;

/// <summary>
/// REQ-142: @state_name 式の字句解析・構文解析
/// </summary>
public class StateReferenceExpressionTests
{
    [Fact]
    public void Tokenizer_AtPrefix_ProducesAtIdentifierToken()
    {
        var tokens = ExpressionTokenizer.Tokenize("@running_status");

        tokens.Should().HaveCount(2); // AtIdentifier + Eof
        tokens[0].Type.Should().Be(ExpressionTokenType.AtIdentifier);
        tokens[0].Value.Should().Be("running_status");
    }

    [Fact]
    public void Tokenizer_AtWithoutName_ThrowsFormatException()
    {
        var act = () => ExpressionTokenizer.Tokenize("@ ");

        act.Should().Throw<FormatException>()
            .WithMessage("*Expected identifier after '@'*");
    }

    [Fact]
    public void Tokenizer_AtAtEndOfInput_ThrowsFormatException()
    {
        var act = () => ExpressionTokenizer.Tokenize("@");

        act.Should().Throw<FormatException>()
            .WithMessage("*Expected identifier after '@'*");
    }

    [Fact]
    public void Parser_AtPrefix_ProducesStateReference()
    {
        var expr = ExpressionParser.Parse("{@name}");

        var stateRef = expr.Root.Should().BeOfType<ExpressionNode.StateReference>().Subject;
        stateRef.StateName.Should().Be("name");
    }

    [Fact]
    public void Parser_AtInComplexExpression()
    {
        // {x & 0x80 ? x : @state}
        var expr = ExpressionParser.Parse("{x & 0x80 ? x : @state}");

        var cond = expr.Root.Should().BeOfType<ExpressionNode.Conditional>().Subject;
        cond.FalseExpr.Should().BeOfType<ExpressionNode.StateReference>()
            .Which.StateName.Should().Be("state");
    }

    [Fact]
    public void Parser_AtInArithmetic()
    {
        var expr = ExpressionParser.Parse("{@offset + 10}");

        var binOp = expr.Root.Should().BeOfType<ExpressionNode.BinaryOp>().Subject;
        binOp.Left.Should().BeOfType<ExpressionNode.StateReference>()
            .Which.StateName.Should().Be("offset");
        binOp.Right.Should().BeOfType<ExpressionNode.LiteralInt>()
            .Which.Value.Should().Be(10);
    }

    [Fact]
    public void Tokenizer_AtWithUnderscore_ProducesAtIdentifierToken()
    {
        var tokens = ExpressionTokenizer.Tokenize("@_my_var");

        tokens[0].Type.Should().Be(ExpressionTokenType.AtIdentifier);
        tokens[0].Value.Should().Be("_my_var");
    }
}
