namespace BinAnalyzer.Core.Validation;

public sealed record ValidationDiagnostic(
    DiagnosticSeverity Severity,
    string Code,
    string Message,
    string? StructName,
    string? FieldName);
