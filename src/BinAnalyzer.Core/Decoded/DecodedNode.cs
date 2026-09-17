namespace BinAnalyzer.Core.Decoded;

public abstract class DecodedNode
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required long Offset { get; init; }
    public required long Size { get; init; }
    public bool? ValidationPassed { get; init; }

    /// <summary>パディングフィールドの場合true。出力時にデフォルト非表示。</summary>
    public bool IsPadding { get; init; }

    /// <summary>カスタムバリデーション式の結果。</summary>
    public ValidationInfo? Validation { get; init; }

    /// <summary>ビットストリームフィールドのバイト内ビットオフセット（0–7）。nullはバイト単位フィールド。</summary>
    public int? BitOffset { get; init; }

    /// <summary>
    /// DSL 上の型（<c>uint32</c> / <c>ascii</c> 等）。値の書き戻し（符号・固定長判定）に使う。
    /// エンジンがスカラー系ノードに設定する。struct / array / 合成ノードは null。
    /// </summary>
    public Models.FieldType? DslType { get; init; }

    /// <summary>DSL の型名（<c>uint32</c> / <c>ascii</c> / <c>struct</c> 等）。<see cref="DslType"/> から導出。未設定なら null。</summary>
    public string? TypeName => DslType is { } t ? Models.FieldTypeNames.ToDslName(t) : null;
}
