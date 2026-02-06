namespace BinAnalyzer.Core.Expressions;

/// <summary>
/// 演算子優先順位対応の再帰下降パーサー。
/// "{...}" DSL文字列から式をパースする。
///
/// 文法（優先順位: 低→高）:
///   or_expr     → and_expr ("or" and_expr)*
///   and_expr    → compare_expr ("and" compare_expr)*
///   compare_expr → add_expr (("==" | "!=" | "&lt;" | "&lt;=" | "&gt;" | "&gt;=") add_expr)?
///   add_expr    → mul_expr (("+" | "-") mul_expr)*
///   mul_expr    → unary_expr (("*" | "/" | "%") unary_expr)*
///   unary_expr  → ("-" | "not") unary_expr | primary
///   primary     → INTEGER | STRING | IDENTIFIER | "(" or_expr ")"
/// </summary>
public sealed class ExpressionParser
{
    private readonly List<ExpressionToken> _tokens;
    private int _pos;

    private ExpressionParser(List<ExpressionToken> tokens)
    {
        _tokens = tokens;
        _pos = 0;
    }

    public static Expression Parse(string text)
    {
        // 波括弧で囲まれている場合は除去: "{expr}" → "expr"
        var stripped = text;
        if (stripped.StartsWith('{') && stripped.EndsWith('}'))
            stripped = stripped[1..^1];

        var tokens = ExpressionTokenizer.Tokenize(stripped);
        var parser = new ExpressionParser(tokens);
        var node = parser.ParseOrExpr();

        if (parser.Current.Type != ExpressionTokenType.Eof)
            throw new FormatException($"Unexpected token '{parser.Current.Value}' at position {parser.Current.Position}");

        return new Expression(node, text);
    }

    private ExpressionToken Current => _tokens[_pos];

    private ExpressionToken Advance()
    {
        var token = _tokens[_pos];
        _pos++;
        return token;
    }

    private bool Match(ExpressionTokenType type)
    {
        if (Current.Type != type)
            return false;
        _pos++;
        return true;
    }

    private ExpressionNode ParseOrExpr()
    {
        var left = ParseAndExpr();
        while (Current.Type == ExpressionTokenType.Or)
        {
            Advance();
            var right = ParseAndExpr();
            left = new ExpressionNode.BinaryOp(left, BinaryOperator.Or, right);
        }
        return left;
    }

    private ExpressionNode ParseAndExpr()
    {
        var left = ParseCompareExpr();
        while (Current.Type == ExpressionTokenType.And)
        {
            Advance();
            var right = ParseCompareExpr();
            left = new ExpressionNode.BinaryOp(left, BinaryOperator.And, right);
        }
        return left;
    }

    private ExpressionNode ParseCompareExpr()
    {
        var left = ParseAddExpr();

        var op = Current.Type switch
        {
            ExpressionTokenType.EqualEqual => (BinaryOperator?)BinaryOperator.Equal,
            ExpressionTokenType.NotEqual => BinaryOperator.NotEqual,
            ExpressionTokenType.LessThan => BinaryOperator.LessThan,
            ExpressionTokenType.LessThanOrEqual => BinaryOperator.LessThanOrEqual,
            ExpressionTokenType.GreaterThan => BinaryOperator.GreaterThan,
            ExpressionTokenType.GreaterThanOrEqual => BinaryOperator.GreaterThanOrEqual,
            _ => null,
        };

        if (op is not null)
        {
            Advance();
            var right = ParseAddExpr();
            left = new ExpressionNode.BinaryOp(left, op.Value, right);
        }

        return left;
    }

    private ExpressionNode ParseAddExpr()
    {
        var left = ParseMulExpr();
        while (Current.Type is ExpressionTokenType.Plus or ExpressionTokenType.Minus)
        {
            var op = Advance().Type == ExpressionTokenType.Plus
                ? BinaryOperator.Add
                : BinaryOperator.Subtract;
            var right = ParseMulExpr();
            left = new ExpressionNode.BinaryOp(left, op, right);
        }
        return left;
    }

    private ExpressionNode ParseMulExpr()
    {
        var left = ParseUnaryExpr();
        while (Current.Type is ExpressionTokenType.Star or ExpressionTokenType.Slash or ExpressionTokenType.Percent)
        {
            var op = Advance().Type switch
            {
                ExpressionTokenType.Star => BinaryOperator.Multiply,
                ExpressionTokenType.Slash => BinaryOperator.Divide,
                _ => BinaryOperator.Modulo,
            };
            var right = ParseUnaryExpr();
            left = new ExpressionNode.BinaryOp(left, op, right);
        }
        return left;
    }

    private ExpressionNode ParseUnaryExpr()
    {
        if (Current.Type == ExpressionTokenType.Minus)
        {
            Advance();
            var operand = ParseUnaryExpr();
            return new ExpressionNode.UnaryOp(UnaryOperator.Negate, operand);
        }

        if (Current.Type == ExpressionTokenType.Not)
        {
            Advance();
            var operand = ParseUnaryExpr();
            return new ExpressionNode.UnaryOp(UnaryOperator.Not, operand);
        }

        return ParsePrimary();
    }

    private ExpressionNode ParsePrimary()
    {
        switch (Current.Type)
        {
            case ExpressionTokenType.Integer:
            {
                var token = Advance();
                var value = token.Value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ToInt64(token.Value[2..], 16)
                    : long.Parse(token.Value);
                return new ExpressionNode.LiteralInt(value);
            }

            case ExpressionTokenType.String:
            {
                var token = Advance();
                return new ExpressionNode.LiteralString(token.Value);
            }

            case ExpressionTokenType.Identifier:
            {
                var token = Advance();
                return new ExpressionNode.FieldReference(token.Value);
            }

            case ExpressionTokenType.LeftParen:
            {
                Advance();
                var expr = ParseOrExpr();
                if (!Match(ExpressionTokenType.RightParen))
                    throw new FormatException($"Expected ')' at position {Current.Position}");
                return expr;
            }

            default:
                throw new FormatException($"Unexpected token '{Current.Value}' at position {Current.Position}");
        }
    }
}
