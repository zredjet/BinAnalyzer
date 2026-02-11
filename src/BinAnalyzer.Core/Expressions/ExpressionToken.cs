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
    Ampersand,
    Pipe,
    Caret,
    LessLess,
    GreaterGreater,
    LeftParen,
    RightParen,
    Comma,
    Question,
    Colon,
    LeftBracket,
    RightBracket,
    Eof,
}

public sealed record ExpressionToken(ExpressionTokenType Type, string Value, int Position);
