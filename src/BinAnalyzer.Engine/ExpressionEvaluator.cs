using BinAnalyzer.Core.Expressions;

namespace BinAnalyzer.Engine;

public static class ExpressionEvaluator
{
    public static object Evaluate(Expression expression, DecodeContext context)
    {
        return EvaluateNode(expression.Root, context);
    }

    public static long EvaluateAsLong(Expression expression, DecodeContext context)
    {
        var result = Evaluate(expression, context);
        return ConvertToLong(result);
    }

    public static string EvaluateAsString(Expression expression, DecodeContext context)
    {
        var result = Evaluate(expression, context);
        return result.ToString() ?? "";
    }

    public static bool EvaluateAsBool(Expression expression, DecodeContext context)
    {
        var result = Evaluate(expression, context);
        return result switch
        {
            bool b => b,
            long l => l != 0,
            int i => i != 0,
            _ => throw new InvalidOperationException($"Cannot convert {result.GetType().Name} to boolean"),
        };
    }

    private static object EvaluateNode(ExpressionNode node, DecodeContext context)
    {
        return node switch
        {
            ExpressionNode.LiteralInt lit => lit.Value,
            ExpressionNode.LiteralString lit => lit.Value,
            ExpressionNode.FieldReference field => ResolveField(field.FieldName, context),
            ExpressionNode.BinaryOp binOp => EvaluateBinaryOp(binOp, context),
            ExpressionNode.UnaryOp unOp => EvaluateUnaryOp(unOp, context),
            ExpressionNode.FunctionCall func => EvaluateFunction(func, context),
            ExpressionNode.IndexAccess idx => ResolveIndexAccess(idx.ArrayName, idx.Index, context),
            ExpressionNode.Conditional cond =>
                ConvertToBool(EvaluateNode(cond.Condition, context))
                    ? EvaluateNode(cond.TrueExpr, context)
                    : EvaluateNode(cond.FalseExpr, context),
            _ => throw new InvalidOperationException($"Unknown expression node type: {node.GetType().Name}"),
        };
    }

    private static object ResolveField(string fieldName, DecodeContext context)
    {
        if (fieldName == "remaining")
            return (long)context.Remaining;

        var value = context.GetVariable(fieldName);
        if (value is null)
            throw new InvalidOperationException($"Variable '{fieldName}' not found in current scope");
        return value;
    }

    private static object ResolveIndexAccess(string arrayName, ExpressionNode indexExpr, DecodeContext context)
    {
        var arrayValue = context.GetVariable(arrayName);
        if (arrayValue is not List<object> list)
            throw new InvalidOperationException(
                $"Variable '{arrayName}' is not an array (actual type: {arrayValue?.GetType().Name ?? "null"})");

        var index = (int)ConvertToLong(EvaluateNode(indexExpr, context));
        if (index < 0 || index >= list.Count)
            throw new InvalidOperationException(
                $"Array index {index} is out of range for '{arrayName}' (length: {list.Count})");

        return list[index];
    }

    private static object EvaluateBinaryOp(ExpressionNode.BinaryOp binOp, DecodeContext context)
    {
        var left = EvaluateNode(binOp.Left, context);
        var right = EvaluateNode(binOp.Right, context);

        return binOp.Operator switch
        {
            BinaryOperator.Add => (object)(ConvertToLong(left) + ConvertToLong(right)),
            BinaryOperator.Subtract => ConvertToLong(left) - ConvertToLong(right),
            BinaryOperator.Multiply => ConvertToLong(left) * ConvertToLong(right),
            BinaryOperator.Divide => ConvertToLong(left) / ConvertToLong(right),
            BinaryOperator.Modulo => ConvertToLong(left) % ConvertToLong(right),
            BinaryOperator.Equal => AreEqual(left, right),
            BinaryOperator.NotEqual => !AreEqual(left, right),
            BinaryOperator.LessThan => ConvertToLong(left) < ConvertToLong(right),
            BinaryOperator.LessThanOrEqual => ConvertToLong(left) <= ConvertToLong(right),
            BinaryOperator.GreaterThan => ConvertToLong(left) > ConvertToLong(right),
            BinaryOperator.GreaterThanOrEqual => ConvertToLong(left) >= ConvertToLong(right),
            BinaryOperator.And => ConvertToBool(left) && ConvertToBool(right),
            BinaryOperator.Or => ConvertToBool(left) || ConvertToBool(right),
            BinaryOperator.BitwiseAnd => (object)(ConvertToLong(left) & ConvertToLong(right)),
            BinaryOperator.BitwiseOr => ConvertToLong(left) | ConvertToLong(right),
            BinaryOperator.BitwiseXor => ConvertToLong(left) ^ ConvertToLong(right),
            BinaryOperator.LeftShift => ConvertToLong(left) << (int)ConvertToLong(right),
            BinaryOperator.RightShift => ConvertToLong(left) >> (int)ConvertToLong(right),
            _ => throw new InvalidOperationException($"Unknown binary operator: {binOp.Operator}"),
        };
    }

    private static object EvaluateUnaryOp(ExpressionNode.UnaryOp unOp, DecodeContext context)
    {
        var operand = EvaluateNode(unOp.Operand, context);
        return unOp.Operator switch
        {
            UnaryOperator.Negate => -ConvertToLong(operand),
            UnaryOperator.Not => !ConvertToBool(operand),
            _ => throw new InvalidOperationException($"Unknown unary operator: {unOp.Operator}"),
        };
    }

    private static object EvaluateFunction(ExpressionNode.FunctionCall func, DecodeContext context)
    {
        return func.Name switch
        {
            "until_marker" => EvaluateUntilMarker(func.Arguments, context),
            "parse_int" => EvaluateParseInt(func.Arguments, context),
            "len" => EvaluateLen(func.Arguments, context),
            "count" => EvaluateLen(func.Arguments, context),
            "min" => EvaluateMinMaxSum(func.Arguments, context, "min"),
            "max" => EvaluateMinMaxSum(func.Arguments, context, "max"),
            "sum" => EvaluateMinMaxSum(func.Arguments, context, "sum"),
            "substr" => EvaluateSubstr(func.Arguments, context),
            "concat" => EvaluateConcat(func.Arguments, context),
            "contains" => EvaluateContains(func.Arguments, context),
            _ => throw new InvalidOperationException($"Unknown function: '{func.Name}'"),
        };
    }

    private static object EvaluateUntilMarker(
        IReadOnlyList<ExpressionNode> args, DecodeContext context)
    {
        if (args.Count == 0)
            throw new InvalidOperationException(
                "until_marker requires at least 1 argument");

        var marker = new byte[args.Count];
        for (var i = 0; i < args.Count; i++)
        {
            var val = EvaluateNode(args[i], context);
            marker[i] = (byte)ConvertToLong(val);
        }

        var markerPos = context.FindMarker(marker);
        if (markerPos < 0)
            return (long)context.Remaining;

        return (long)(markerPos - context.Position);
    }

    private static object EvaluateParseInt(
        IReadOnlyList<ExpressionNode> args, DecodeContext context)
    {
        if (args.Count != 2)
            throw new InvalidOperationException(
                "parse_int requires exactly 2 arguments: parse_int(field, base)");

        var rawValue = EvaluateNode(args[0], context);
        var stringValue = rawValue?.ToString() ?? "";

        var baseValue = (int)ConvertToLong(EvaluateNode(args[1], context));
        if (baseValue is not (2 or 8 or 10 or 16))
            throw new InvalidOperationException(
                $"parse_int base must be 2, 8, 10, or 16, got: {baseValue}");

        var trimmed = stringValue.TrimEnd('\0', ' ');
        if (trimmed.Length == 0)
            return 0L;

        try
        {
            return Convert.ToInt64(trimmed, baseValue);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            return 0L;
        }
    }

    private static object EvaluateLen(
        IReadOnlyList<ExpressionNode> args, DecodeContext context)
    {
        if (args.Count != 1)
            throw new InvalidOperationException(
                "len requires exactly 1 argument");

        var value = EvaluateNode(args[0], context);
        if (value is not List<object> list)
            throw new InvalidOperationException(
                $"Argument to len() is not an array (actual type: {value?.GetType().Name ?? "null"})");

        return (long)list.Count;
    }

    private static object EvaluateMinMaxSum(
        IReadOnlyList<ExpressionNode> args, DecodeContext context, string operation)
    {
        if (args.Count != 1)
            throw new InvalidOperationException(
                $"{operation} requires exactly 1 argument");

        var value = EvaluateNode(args[0], context);
        if (value is not List<object> list)
            throw new InvalidOperationException(
                $"Argument to {operation}() is not an array (actual type: {value?.GetType().Name ?? "null"})");

        if (list.Count == 0)
        {
            if (operation == "sum")
                return 0L;
            throw new InvalidOperationException(
                $"{operation}() cannot operate on an empty array");
        }

        var result = ConvertToLong(list[0]);
        for (var i = 1; i < list.Count; i++)
        {
            var current = ConvertToLong(list[i]);
            result = operation switch
            {
                "min" => current < result ? current : result,
                "max" => current > result ? current : result,
                "sum" => result + current,
                _ => throw new InvalidOperationException($"Unknown operation: {operation}"),
            };
        }

        return result;
    }

    private static object EvaluateSubstr(
        IReadOnlyList<ExpressionNode> args, DecodeContext context)
    {
        if (args.Count != 3)
            throw new InvalidOperationException(
                "substr requires exactly 3 arguments: substr(string, start, length)");

        var value = EvaluateNode(args[0], context);
        if (value is not string str)
            throw new InvalidOperationException(
                $"First argument to substr() must be a string (actual type: {value?.GetType().Name ?? "null"})");

        var start = (int)ConvertToLong(EvaluateNode(args[1], context));
        var length = (int)ConvertToLong(EvaluateNode(args[2], context));

        if (start >= str.Length || start < 0)
            return "";

        if (start + length > str.Length)
            length = str.Length - start;

        return str.Substring(start, length);
    }

    private static object EvaluateConcat(
        IReadOnlyList<ExpressionNode> args, DecodeContext context)
    {
        if (args.Count < 2)
            throw new InvalidOperationException(
                "concat requires at least 2 arguments");

        var sb = new System.Text.StringBuilder();
        foreach (var arg in args)
        {
            var value = EvaluateNode(arg, context);
            sb.Append(value?.ToString() ?? "");
        }

        return sb.ToString();
    }

    private static object EvaluateContains(
        IReadOnlyList<ExpressionNode> args, DecodeContext context)
    {
        if (args.Count != 2)
            throw new InvalidOperationException(
                "contains requires exactly 2 arguments: contains(string, search)");

        var value = EvaluateNode(args[0], context);
        if (value is not string str)
            throw new InvalidOperationException(
                $"First argument to contains() must be a string (actual type: {value?.GetType().Name ?? "null"})");

        var searchValue = EvaluateNode(args[1], context);
        if (searchValue is not string search)
            throw new InvalidOperationException(
                $"Second argument to contains() must be a string (actual type: {searchValue?.GetType().Name ?? "null"})");

        return str.Contains(search);
    }

    private static long ConvertToLong(object value) => value switch
    {
        long l => l,
        int i => i,
        uint u => u,
        byte b => b,
        sbyte sb => sb,
        short s => s,
        ushort us => us,
        ulong ul => (long)ul,
        _ => throw new InvalidOperationException($"Cannot convert {value.GetType().Name} ('{value}') to integer"),
    };

    private static bool ConvertToBool(object value) => value switch
    {
        bool b => b,
        long l => l != 0,
        int i => i != 0,
        _ => throw new InvalidOperationException($"Cannot convert {value.GetType().Name} to boolean"),
    };

    private static bool AreEqual(object left, object right)
    {
        if (left is string ls && right is string rs)
            return ls == rs;

        // Try numeric comparison
        try
        {
            return ConvertToLong(left) == ConvertToLong(right);
        }
        catch
        {
            return Equals(left, right);
        }
    }
}
