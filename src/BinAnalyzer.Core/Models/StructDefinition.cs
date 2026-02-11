using BinAnalyzer.Core.Expressions;

namespace BinAnalyzer.Core.Models;

public sealed class StructDefinition
{
    public required string Name { get; init; }
    public required IReadOnlyList<FieldDefinition> Fields { get; init; }

    /// <summary>この構造体のインスタンス開始位置を指定バイト境界にアラインする（繰り返し時）。</summary>
    public int? Align { get; init; }

    /// <summary>この構造体内のフィールドに適用するエンディアン。nullの場合は親スコープまたはフォーマットデフォルトを使用。</summary>
    public Endianness? Endianness { get; init; }

    /// <summary>実行時に評価するエンディアン式。結果は 'little'/'big' 文字列。Endiannessと相互排他。</summary>
    public Expression? EndiannessExpression { get; init; }

    /// <summary>trueの場合、この構造体のデコード結果のバイト列を文字列テーブルとして登録する。</summary>
    public bool IsStringTable { get; init; }
}
