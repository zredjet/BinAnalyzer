using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;

namespace BinAnalyzer.Core.Validation;

/// <summary>
/// 式の中の名前と関数名の検査（VAL124 / VAL125。REQ-189）の材料: 式から参照できる名前の集合、フィールドが持つ式、式の中の参照。
/// </summary>
/// <remarks>
/// エンジンは入れ子の struct の値を親のスコープへ昇格するので、式から見える名前は参照関係とデコード順で決まり、
/// 静的には決めにくい。ここでは定義全体の名前の集合で近似する。「どこにも無い名前」は見つかるが、
/// 「定義にはあるがその式からは見えない名前」は見つからない（従来どおりデコード時のエラーになる）。
/// </remarks>
internal static class ExpressionReferences
{
    /// <summary>エンジンが実行時に束縛する名前（繰り返しの <c>_index</c> / <c>_prev</c>、評価器が特別扱いする <c>remaining</c> / <c>_offset</c>）。</summary>
    internal static readonly IReadOnlyList<string> SpecialVariables = ["_index", "_prev", "remaining", "_offset"];

    /// <summary>
    /// 式から参照できる名前（定義順、重複なし）: 全 struct のフィールド名・bitfield のエントリ名・テンプレートのパラメータ名と特殊変数。
    /// エンジンが <c>SetVariable</c> する名前と対応する（flags のフィールドは変数として束縛されないので含めない）。
    /// </summary>
    public static List<string> CollectNames(FormatDefinition format)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(string name)
        {
            if (seen.Add(name))
                names.Add(name);
        }

        foreach (var structDef in format.Structs.Values)
        {
            foreach (var parameter in structDef.Parameters)
                Add(parameter.Name);
            foreach (var field in structDef.Fields)
            {
                Add(field.Name);
                if (field.BitfieldEntries is { } entries)
                {
                    foreach (var entry in entries)
                        Add(entry.Name);
                }
            }
        }
        foreach (var special in SpecialVariables)
            Add(special);
        return names;
    }

    /// <summary>フィールドが持つ式と、その DSL のキー名（メッセージ用）。</summary>
    public static IEnumerable<(string Key, Expression Expr)> ExpressionsOf(FieldDefinition field)
    {
        if (field.SizeExpression is { } size) yield return ("size", size);
        if (field.ElementSizeExpression is { } elementSize) yield return ("element_size", elementSize);
        if (field.Condition is { } condition) yield return ("if", condition);
        if (field.ValueExpression is { } value) yield return ("value", value);
        if (field.SeekExpression is { } seek) yield return ("seek", seek);
        if (field.SeekBaseExpression is { } seekBase) yield return ("seek_base", seekBase);
        if (field.ValidationExpression is { } validate) yield return ("validate", validate);
        if (field.SwitchOn is { } switchOn) yield return ("switch_on", switchOn);
        if (field.SwitchCases is { } cases)
        {
            foreach (var c in cases)
                yield return ("cases", c.Condition);
        }
        switch (field.Repeat)
        {
            case RepeatMode.Count count:
                yield return ("repeat_count", count.CountExpression);
                break;
            case RepeatMode.UntilValue until:
                yield return ("repeat_until", until.Condition);
                break;
            case RepeatMode.While whileMode:
                yield return ("repeat_while", whileMode.Condition);
                break;
        }
        if (field.RepeatMax is { } repeatMax) yield return ("repeat_max", repeatMax);
        if (field.RepeatErrorLimit is { } errorLimit) yield return ("repeat_error_limit", errorLimit);
        if (field.StateIf is { } stateIf) yield return ("state_if", stateIf);
        if (field.StructArgs is { } args)
        {
            foreach (var arg in args)
            {
                if (arg.Expression is { } argExpr)
                    yield return ("struct の引数", argExpr);
            }
        }
        if (field.Checksum is { } checksum)
        {
            IEnumerable<ChecksumRange> ranges = checksum.Ranges ?? [];
            if (checksum.Range is { } range)
                ranges = ranges.Prepend(range);
            foreach (var r in ranges)
            {
                yield return ("checksum の range の offset", r.OffsetExpression);
                yield return ("checksum の range の size", r.SizeExpression);
            }
        }
    }

    /// <summary>式の木の中の名前参照（<c>isFunction: false</c>）と関数呼び出し（<c>true</c>）を出現順に列挙する。</summary>
    public static IEnumerable<(string Name, bool IsFunction)> References(ExpressionNode node)
    {
        switch (node)
        {
            case ExpressionNode.FieldReference f:
                yield return (f.FieldName, false);
                break;
            case ExpressionNode.IndexAccess ia:
                yield return (ia.ArrayName, false);
                foreach (var r in References(ia.Index)) yield return r;
                break;
            case ExpressionNode.MemberAccess ma:
                // メンバー名は検査しない（先頭の名前は対象の式の中で検査される）
                foreach (var r in References(ma.Object)) yield return r;
                break;
            case ExpressionNode.ElementAccess ea:
                foreach (var r in References(ea.Array)) yield return r;
                foreach (var r in References(ea.Index)) yield return r;
                break;
            case ExpressionNode.FunctionCall fc:
                yield return (fc.Name, true);
                foreach (var arg in fc.Arguments)
                {
                    foreach (var r in References(arg)) yield return r;
                }
                break;
            case ExpressionNode.BinaryOp b:
                foreach (var r in References(b.Left)) yield return r;
                foreach (var r in References(b.Right)) yield return r;
                break;
            case ExpressionNode.UnaryOp u:
                foreach (var r in References(u.Operand)) yield return r;
                break;
            case ExpressionNode.Conditional c:
                foreach (var r in References(c.Condition)) yield return r;
                foreach (var r in References(c.TrueExpr)) yield return r;
                foreach (var r in References(c.FalseExpr)) yield return r;
                break;
            // LiteralInt / LiteralString / StateReference（@name）は名前を参照しない
        }
    }
}
