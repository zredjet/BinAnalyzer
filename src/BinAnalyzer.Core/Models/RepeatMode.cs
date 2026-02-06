using BinAnalyzer.Core.Expressions;

namespace BinAnalyzer.Core.Models;

public abstract record RepeatMode
{
    private RepeatMode() { }

    public sealed record None : RepeatMode;
    public sealed record Count(Expression CountExpression) : RepeatMode;
    public sealed record UntilEof : RepeatMode;
    public sealed record UntilValue(Expression Condition) : RepeatMode;
}
