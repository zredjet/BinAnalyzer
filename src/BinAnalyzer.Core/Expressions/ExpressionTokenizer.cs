namespace BinAnalyzer.Core.Expressions;

public static class ExpressionTokenizer
{
    public static List<ExpressionToken> Tokenize(string input)
    {
        var tokens = new List<ExpressionToken>();
        var i = 0;

        while (i < input.Length)
        {
            var ch = input[i];

            if (char.IsWhiteSpace(ch))
            {
                i++;
                continue;
            }

            if (char.IsDigit(ch))
            {
                var start = i;
                if (i + 1 < input.Length && ch == '0' && input[i + 1] == 'x')
                {
                    i += 2;
                    while (i < input.Length && IsHexDigit(input[i]))
                        i++;
                }
                else
                {
                    while (i < input.Length && char.IsDigit(input[i]))
                        i++;
                }
                tokens.Add(new ExpressionToken(ExpressionTokenType.Integer, input[start..i], start));
                continue;
            }

            if (ch == '\'')
            {
                var start = i;
                i++;
                while (i < input.Length && input[i] != '\'')
                    i++;
                if (i >= input.Length)
                    throw new FormatException($"Unterminated string literal at position {start}");
                i++; // closing quote
                var value = input[(start + 1)..(i - 1)];
                tokens.Add(new ExpressionToken(ExpressionTokenType.String, value, start));
                continue;
            }

            if (char.IsLetter(ch) || ch == '_')
            {
                var start = i;
                while (i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] == '_'))
                    i++;
                var word = input[start..i];
                var type = word switch
                {
                    "and" => ExpressionTokenType.And,
                    "or" => ExpressionTokenType.Or,
                    "not" => ExpressionTokenType.Not,
                    _ => ExpressionTokenType.Identifier,
                };
                tokens.Add(new ExpressionToken(type, word, start));
                continue;
            }

            switch (ch)
            {
                case '+':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.Plus, "+", i));
                    i++;
                    break;
                case '-':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.Minus, "-", i));
                    i++;
                    break;
                case '*':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.Star, "*", i));
                    i++;
                    break;
                case '/':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.Slash, "/", i));
                    i++;
                    break;
                case '%':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.Percent, "%", i));
                    i++;
                    break;
                case '(':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.LeftParen, "(", i));
                    i++;
                    break;
                case ')':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.RightParen, ")", i));
                    i++;
                    break;
                case ',':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.Comma, ",", i));
                    i++;
                    break;
                case '=' when Peek(input, i + 1) == '=':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.EqualEqual, "==", i));
                    i += 2;
                    break;
                case '!' when Peek(input, i + 1) == '=':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.NotEqual, "!=", i));
                    i += 2;
                    break;
                case '&':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.Ampersand, "&", i));
                    i++;
                    break;
                case '|':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.Pipe, "|", i));
                    i++;
                    break;
                case '^':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.Caret, "^", i));
                    i++;
                    break;
                case '<' when Peek(input, i + 1) == '<':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.LessLess, "<<", i));
                    i += 2;
                    break;
                case '<' when Peek(input, i + 1) == '=':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.LessThanOrEqual, "<=", i));
                    i += 2;
                    break;
                case '<':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.LessThan, "<", i));
                    i++;
                    break;
                case '>' when Peek(input, i + 1) == '>':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.GreaterGreater, ">>", i));
                    i += 2;
                    break;
                case '>' when Peek(input, i + 1) == '=':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.GreaterThanOrEqual, ">=", i));
                    i += 2;
                    break;
                case '>':
                    tokens.Add(new ExpressionToken(ExpressionTokenType.GreaterThan, ">", i));
                    i++;
                    break;
                default:
                    throw new FormatException($"Unexpected character '{ch}' at position {i}");
            }
        }

        tokens.Add(new ExpressionToken(ExpressionTokenType.Eof, "", i));
        return tokens;
    }

    private static char Peek(string input, int index) =>
        index < input.Length ? input[index] : '\0';

    private static bool IsHexDigit(char ch) =>
        char.IsDigit(ch) || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
}
