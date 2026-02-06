namespace BinAnalyzer.Core.Expressions;

public abstract record ExpressionNode
{
    private ExpressionNode() { }

    public sealed record LiteralInt(long Value) : ExpressionNode;
    public sealed record LiteralString(string Value) : ExpressionNode;
    public sealed record FieldReference(string FieldName) : ExpressionNode;
    public sealed record BinaryOp(ExpressionNode Left, BinaryOperator Operator, ExpressionNode Right) : ExpressionNode;
    public sealed record UnaryOp(UnaryOperator Operator, ExpressionNode Operand) : ExpressionNode;
}

public enum BinaryOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    And,
    Or,
    BitwiseAnd,
    BitwiseOr,
    BitwiseXor,
    LeftShift,
    RightShift,
}

public enum UnaryOperator
{
    Negate,
    Not,
}
