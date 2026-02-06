namespace BinAnalyzer.Core.Expressions;

public enum ExpressionTokenType
{
    Integer,
    String,
    Identifier,
    Plus,
    Minus,
    Star,
    Slash,
    Percent,
    EqualEqual,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    And,
    Or,
    Not,
    LeftParen,
    RightParen,
    Eof,
}

public sealed record ExpressionToken(ExpressionTokenType Type, string Value, int Position);
