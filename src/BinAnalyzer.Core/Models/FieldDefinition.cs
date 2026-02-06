using BinAnalyzer.Core.Expressions;

namespace BinAnalyzer.Core.Models;

public sealed class FieldDefinition
{
    public required string Name { get; init; }
    public required FieldType Type { get; init; }

    /// <summary>固定バイトサイズ。式で決定される場合はnull。</summary>
    public int? Size { get; init; }

    /// <summary>動的サイズの式（例: "{length}"）。</summary>
    public Expression? SizeExpression { get; init; }

    /// <summary>現在のバウンダリスコープ内の残りバイト数を使用する。</summary>
    public bool SizeRemaining { get; init; }

    public string? EnumRef { get; init; }
    public string? FlagsRef { get; init; }
    public string? StructRef { get; init; }

    public RepeatMode Repeat { get; init; } = new RepeatMode.None();

    /// <summary>switch型フィールドの分岐ケース。</summary>
    public Expression? SwitchOn { get; init; }
    public IReadOnlyList<SwitchCase>? SwitchCases { get; init; }
    public string? SwitchDefault { get; init; }

    /// <summary>bitfield型フィールドのエントリ定義。</summary>
    public IReadOnlyList<BitfieldEntry>? BitfieldEntries { get; init; }

    /// <summary>チェックサム検証仕様。</summary>
    public ChecksumSpec? Checksum { get; init; }

    /// <summary>バリデーション用の期待バイト値（例: PNGシグネチャ）。</summary>
    public byte[]? Expected { get; init; }

    /// <summary>条件式。falseの場合フィールドをスキップする。</summary>
    public Expression? Condition { get; init; }

    public string? Description { get; init; }

    /// <summary>フィールドデコード後、次フィールドの開始位置を指定バイト境界にアラインする。</summary>
    public int? Align { get; init; }

    /// <summary>パディングフィールドとして扱い、出力時にデフォルト非表示とする。</summary>
    public bool IsPadding { get; init; }

    /// <summary>繰り返しフィールドの各要素の固定バイトサイズ。式で決定される場合はnull。</summary>
    public int? ElementSize { get; init; }

    /// <summary>繰り返しフィールドの各要素サイズの動的式（例: "{entry_size}"）。</summary>
    public Expression? ElementSizeExpression { get; init; }

    /// <summary>virtual型フィールドの値を計算する式。</summary>
    public Expression? ValueExpression { get; init; }

    /// <summary>フィールドデコード前にジャンプする絶対オフセットの式。</summary>
    public Expression? SeekExpression { get; init; }

    /// <summary>trueの場合、seekしたフィールドのデコード後に元の位置に復帰する。</summary>
    public bool SeekRestore { get; init; }

    /// <summary>このフィールドに適用するエンディアン。nullの場合は構造体またはフォーマットデフォルトを使用。</summary>
    public Endianness? Endianness { get; init; }

    /// <summary>デコード後に評価するバリデーション式。</summary>
    public Expression? ValidationExpression { get; init; }

    /// <summary>参照する文字列テーブル名。整数値をオフセットとして文字列を解決する。</summary>
    public string? StringTableRef { get; init; }
}
