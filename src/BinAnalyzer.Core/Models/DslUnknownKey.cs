namespace BinAnalyzer.Core.Models;

/// <summary>
/// フォーマット定義の YAML にあった、DSL が受け付けないキー（REQ-184）。ローダーは読み飛ばし、検証器が VAL123 の警告にする。
/// </summary>
/// <param name="Key">キー名（例: <c>expect</c>）。</param>
/// <param name="Context">キーがあった場所の説明（例: <c>struct 's' のフィールド 'magic'</c>）。</param>
/// <param name="StructName">struct 定義の中なら struct 名。</param>
/// <param name="FieldName">struct のフィールド定義の中ならフィールド名。</param>
/// <param name="Suggestion">同じ場所で受け付けるキーのうち近いもの（例: <c>expected</c>）。無ければ null。</param>
/// <param name="SourceFile">定義元の識別子（ファイルパス等）。文字列から読んだ場合は null。</param>
/// <param name="SourceLine">キーの行（1 始まり）。</param>
public sealed record DslUnknownKey(
    string Key,
    string Context,
    string? StructName,
    string? FieldName,
    string? Suggestion,
    string? SourceFile,
    int? SourceLine);
