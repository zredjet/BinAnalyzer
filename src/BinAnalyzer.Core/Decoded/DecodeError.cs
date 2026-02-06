namespace BinAnalyzer.Core.Decoded;

public sealed record DecodeError(string Message, long Offset, string FieldPath, string? FieldType);
