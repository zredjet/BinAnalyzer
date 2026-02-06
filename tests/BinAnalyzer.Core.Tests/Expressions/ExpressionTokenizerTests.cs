using BinAnalyzer.Core.Expressions;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Expressions;

public class ExpressionTokenizerTests
{
    [Fact]
    public void Tokenize_SimpleInteger()
    {
        var tokens = ExpressionTokenizer.Tokenize("42");
        tokens.Should().HaveCount(2);
        tokens[0].Type.Should().Be(ExpressionTokenType.Integer);
        tokens[0].Value.Should().Be("42");
        tokens[1].Type.Should().Be(ExpressionTokenType.Eof);
    }

    [Fact]
    public void Tokenize_HexInteger()
    {
        var tokens = ExpressionTokenizer.Tokenize("0xFF");
        tokens.Should().HaveCount(2);
        tokens[0].Type.Should().Be(ExpressionTokenType.Integer);
        tokens[0].Value.Should().Be("0xFF");
    }

    [Fact]
    public void Tokenize_StringLiteral()
    {
        var tokens = ExpressionTokenizer.Tokenize("'IHDR'");
        tokens.Should().HaveCount(2);
        tokens[0].Type.Should().Be(ExpressionTokenType.String);
        tokens[0].Value.Should().Be("IHDR");
    }

    [Fact]
    public void Tokenize_Identifier()
    {
        var tokens = ExpressionTokenizer.Tokenize("length");
        tokens.Should().HaveCount(2);
        tokens[0].Type.Should().Be(ExpressionTokenType.Identifier);
        tokens[0].Value.Should().Be("length");
    }

    [Fact]
    public void Tokenize_ArithmeticExpression()
    {
        var tokens = ExpressionTokenizer.Tokenize("length - 4");
        tokens.Should().HaveCount(4);
        tokens[0].Type.Should().Be(ExpressionTokenType.Identifier);
        tokens[1].Type.Should().Be(ExpressionTokenType.Minus);
        tokens[2].Type.Should().Be(ExpressionTokenType.Integer);
        tokens[3].Type.Should().Be(ExpressionTokenType.Eof);
    }

    [Fact]
    public void Tokenize_ComparisonExpression()
    {
        var tokens = ExpressionTokenizer.Tokenize("type == 'IHDR'");
        tokens.Should().HaveCount(4);
        tokens[0].Type.Should().Be(ExpressionTokenType.Identifier);
        tokens[1].Type.Should().Be(ExpressionTokenType.EqualEqual);
        tokens[2].Type.Should().Be(ExpressionTokenType.String);
        tokens[3].Type.Should().Be(ExpressionTokenType.Eof);
    }

    [Fact]
    public void Tokenize_AllOperators()
    {
        var tokens = ExpressionTokenizer.Tokenize("+ - * / % == != < <= > >=");
        tokens.Select(t => t.Type).Should().ContainInOrder(
            ExpressionTokenType.Plus,
            ExpressionTokenType.Minus,
            ExpressionTokenType.Star,
            ExpressionTokenType.Slash,
            ExpressionTokenType.Percent,
            ExpressionTokenType.EqualEqual,
            ExpressionTokenType.NotEqual,
            ExpressionTokenType.LessThan,
            ExpressionTokenType.LessThanOrEqual,
            ExpressionTokenType.GreaterThan,
            ExpressionTokenType.GreaterThanOrEqual);
    }

    [Fact]
    public void Tokenize_LogicalKeywords()
    {
        var tokens = ExpressionTokenizer.Tokenize("a and b or not c");
        tokens[0].Type.Should().Be(ExpressionTokenType.Identifier);
        tokens[1].Type.Should().Be(ExpressionTokenType.And);
        tokens[2].Type.Should().Be(ExpressionTokenType.Identifier);
        tokens[3].Type.Should().Be(ExpressionTokenType.Or);
        tokens[4].Type.Should().Be(ExpressionTokenType.Not);
        tokens[5].Type.Should().Be(ExpressionTokenType.Identifier);
    }

    [Fact]
    public void Tokenize_Parentheses()
    {
        var tokens = ExpressionTokenizer.Tokenize("(a + b)");
        tokens[0].Type.Should().Be(ExpressionTokenType.LeftParen);
        tokens[4].Type.Should().Be(ExpressionTokenType.RightParen);
    }

    [Fact]
    public void Tokenize_UnterminatedString_Throws()
    {
        var act = () => ExpressionTokenizer.Tokenize("'unterminated");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Tokenize_UnexpectedCharacter_Throws()
    {
        var act = () => ExpressionTokenizer.Tokenize("@");
        act.Should().Throw<FormatException>();
    }
}
