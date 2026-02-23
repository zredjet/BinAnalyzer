using System.Text.Json;
using BinAnalyzer.Core;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;

namespace BinAnalyzer.Cli;

internal sealed class CliErrorReporter
{
    private readonly bool _useJson;

    public CliErrorReporter(string errorFormat)
    {
        _useJson = errorFormat == "json";
    }

    public void ReportError(string message)
    {
        if (_useJson)
        {
            WriteJsonToStderr(writer =>
            {
                writer.WriteString("type", "error");
                writer.WriteString("message", message);
            });
        }
        else
        {
            Console.Error.WriteLine($"エラー: {message}");
        }
    }

    public void ReportDecodeException(DecodeException dex)
    {
        if (_useJson)
        {
            WriteJsonToStderr(writer =>
            {
                writer.WriteString("type", "decode_error");
                writer.WriteString("message", dex.Message);
                writer.WriteNumber("offset", dex.Offset);
                writer.WriteString("offset_hex", $"0x{dex.Offset:X8}");
                writer.WriteString("field_path", dex.FieldPath);
                if (dex.FieldType is not null)
                    writer.WriteString("field_type", dex.FieldType);
                if (dex.Hint is not null)
                    writer.WriteString("hint", dex.Hint);
            });
        }
        else
        {
            Console.Error.Write(dex.FormatMessage());
        }
    }

    public void ReportValidationResult(ValidationResult result)
    {
        if (_useJson)
        {
            WriteJsonToStderr(writer =>
            {
                writer.WriteString("type", "validation");

                writer.WriteStartArray("errors");
                foreach (var error in result.Errors)
                {
                    writer.WriteStartObject();
                    writer.WriteString("code", error.Code);
                    writer.WriteString("message", error.Message);
                    if (error.StructName is not null)
                        writer.WriteString("struct", error.StructName);
                    if (error.FieldName is not null)
                        writer.WriteString("field", error.FieldName);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                writer.WriteStartArray("warnings");
                foreach (var warning in result.Warnings)
                {
                    writer.WriteStartObject();
                    writer.WriteString("code", warning.Code);
                    writer.WriteString("message", warning.Message);
                    if (warning.StructName is not null)
                        writer.WriteString("struct", warning.StructName);
                    if (warning.FieldName is not null)
                        writer.WriteString("field", warning.FieldName);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            });
        }
        else
        {
            foreach (var warning in result.Warnings)
                Console.Error.WriteLine($"警告 [{warning.Code}]: {warning.Message}");
            foreach (var error in result.Errors)
                Console.Error.WriteLine($"エラー [{error.Code}]: {error.Message}");
        }
    }

    public void ReportDecodeErrors(IReadOnlyList<DecodeError> errors)
    {
        if (_useJson)
        {
            foreach (var err in errors)
            {
                WriteJsonToStderr(writer =>
                {
                    writer.WriteString("type", "decode_error");
                    writer.WriteString("message", err.Message);
                    writer.WriteNumber("offset", err.Offset);
                    writer.WriteString("offset_hex", $"0x{err.Offset:X8}");
                    writer.WriteString("field_path", err.FieldPath);
                    if (err.FieldType is not null)
                        writer.WriteString("field_type", err.FieldType);
                });
            }
        }
        else
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"--- {errors.Count} 件のデコードエラー ---");
            foreach (var err in errors)
            {
                Console.Error.WriteLine($"  [{err.FieldPath}] 0x{err.Offset:X8}: {err.Message}");
            }
        }
    }

    private static void WriteJsonToStderr(Action<Utf8JsonWriter> writeContent)
    {
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            writeContent(writer);
            writer.WriteEndObject();
        }
        Console.Error.WriteLine(System.Text.Encoding.UTF8.GetString(ms.ToArray()));
    }
}
