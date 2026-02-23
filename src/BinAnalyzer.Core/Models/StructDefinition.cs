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

    /// <summary>非nullの場合、この構造体のデコード結果のバイト列を指定エンコーディングの文字列テーブルとして登録する。</summary>
    public StringTableEncoding? StringTableEncoding { get; init; }

    /// <summary>trueの場合、この構造体はビットストリームモードで、フィールドのsizeはビット単位。</summary>
    public bool IsBitstream { get; init; }

    /// <summary>ビットストリームモードのビットオーダー。nullの場合はMSB-first（デフォルト）。</summary>
    public BitOrder? BitOrder { get; init; }

    /// <summary>エラー回復時の再同期マーカーバイトパターン。</summary>
    public byte[]? ResyncMarker { get; init; }

    /// <summary>テンプレートパラメータ定義。パラメータなしstructは空リスト。</summary>
    public IReadOnlyList<TemplateParameter> Parameters { get; init; } = Array.Empty<TemplateParameter>();
}
