using System.Text;

namespace BinAnalyzer.Core;

public sealed class DecodeException : Exception
{
    public long Offset { get; }
    public string FieldPath { get; }
    public string? FieldType { get; }
    public string? Hint { get; }

    public DecodeException(
        string message, long offset, string fieldPath,
        string? fieldType = null, string? hint = null,
        Exception? inner = null)
        : base(message, inner)
    {
        Offset = offset;
        FieldPath = fieldPath;
        FieldType = fieldType;
        Hint = hint;
    }

    public string FormatMessage()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"デコードエラー: {Message}");
        sb.AppendLine($"  場所: {FieldPath}");
        sb.AppendLine($"  オフセット: 0x{Offset:X8} ({Offset})");
        if (FieldType is not null)
            sb.AppendLine($"  フィールド型: {FieldType}");
        if (Hint is not null)
            sb.AppendLine($"  ヒント: {Hint}");
        return sb.ToString();
    }
}
