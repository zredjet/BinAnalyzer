namespace BinAnalyzer.Core.Validation;

public sealed class ValidationResult
{
    public IReadOnlyList<ValidationDiagnostic> Diagnostics { get; }

    public bool IsValid => !Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    public IEnumerable<ValidationDiagnostic> Errors =>
        Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

    public IEnumerable<ValidationDiagnostic> Warnings =>
        Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning);

    public ValidationResult(IReadOnlyList<ValidationDiagnostic> diagnostics)
    {
        Diagnostics = diagnostics;
    }
}
