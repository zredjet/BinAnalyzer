namespace BinAnalyzer.Core.Validation;

public sealed record ValidationDiagnostic(
    DiagnosticSeverity Severity,
    string Code,
    string Message,
    string? StructName,
    string? FieldName,
    string? SourceFile = null,
    int? SourceLine = null)
{
    /// <summary>定義元の位置（<c>png.bdef.yaml:42</c>、ファイル不明なら <c>line 42</c>）。行が分からなければ null。</summary>
    public string? Location => SourceLine is { } line
        ? SourceFile is { } file ? $"{System.IO.Path.GetFileName(file)}:{line}" : $"line {line}"
        : null;

    /// <summary>メッセージに位置を添えたもの（<c>... (png.bdef.yaml:42)</c>）。</summary>
    public string MessageWithLocation => Location is { } loc ? $"{Message} ({loc})" : Message;
}
