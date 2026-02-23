using BinAnalyzer.Core.Expressions;

namespace BinAnalyzer.Core.Models;

/// <summary>
/// テンプレート構造体の参照時に指定する引数。
/// ParameterName — 名前付き引数の場合のパラメータ名。位置引数はnull。
/// Value — 整数リテラル引数の場合の値。
/// Expression — 式引数の場合（{field_ref} 等）。
/// </summary>
public sealed record StructArgument(string? ParameterName, long? Value, Expression? Expression);
