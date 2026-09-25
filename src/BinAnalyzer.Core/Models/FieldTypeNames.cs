namespace BinAnalyzer.Core.Models;

/// <summary>
/// DSL の <c>type:</c> 文字列と <see cref="FieldType"/> の対応。
/// 正規名（<see cref="ToDslName"/>）は 1 つ、解析（<see cref="TryParse"/>）は別名（<c>u32</c> / <c>f32</c> 等）も受け付ける。
/// </summary>
public static class FieldTypeNames
{
    /// <summary>DSL の正規の型名（<c>uint32</c> / <c>ascii</c> / <c>zlib</c> 等）。</summary>
    public static string ToDslName(FieldType type) => type switch
    {
        FieldType.UInt8 => "uint8",
        FieldType.UInt16 => "uint16",
        FieldType.UInt32 => "uint32",
        FieldType.UInt64 => "uint64",
        FieldType.Int8 => "int8",
        FieldType.Int16 => "int16",
        FieldType.Int32 => "int32",
        FieldType.Int64 => "int64",
        FieldType.Bytes => "bytes",
        FieldType.Ascii => "ascii",
        FieldType.Utf8 => "utf8",
        FieldType.Utf16Le => "utf16le",
        FieldType.Utf16Be => "utf16be",
        FieldType.ShiftJis => "sjis",
        FieldType.Latin1 => "latin1",
        FieldType.AsciiZ => "asciiz",
        FieldType.Utf8Z => "utf8z",
        FieldType.Float16 => "float16",
        FieldType.Float32 => "float32",
        FieldType.Float64 => "float64",
        FieldType.Struct => "struct",
        FieldType.Switch => "switch",
        FieldType.Bitfield => "bitfield",
        FieldType.Zlib => "zlib",
        FieldType.Deflate => "deflate",
        FieldType.Gzip => "gzip",
        FieldType.Bzip2 => "bzip2",
        FieldType.Lzma => "lzma",
        FieldType.Zstd => "zstd",
        FieldType.Lz4 => "lz4",
        FieldType.Virtual => "virtual",
        FieldType.ULeb128 => "uleb128",
        FieldType.SLeb128 => "sleb128",
        FieldType.Vlq => "vlq",
        _ => type.ToString().ToLowerInvariant(),
    };

    /// <summary>表示用の短いラベル。整数は <c>u32</c> / <c>i16</c>、浮動小数点は <c>f16</c> / <c>f32</c> / <c>f64</c>、それ以外は正規名。</summary>
    public static string ShortLabel(FieldType type) => type switch
    {
        FieldType.UInt8 => "u8",
        FieldType.UInt16 => "u16",
        FieldType.UInt32 => "u32",
        FieldType.UInt64 => "u64",
        FieldType.Int8 => "i8",
        FieldType.Int16 => "i16",
        FieldType.Int32 => "i32",
        FieldType.Int64 => "i64",
        FieldType.Float16 => "f16",
        FieldType.Float32 => "f32",
        FieldType.Float64 => "f64",
        _ => ToDslName(type),
    };

    /// <summary>DSL の型名（別名を含む、大文字小文字無視）を解析する。</summary>
    public static bool TryParse(string text, out FieldType type)
    {
        FieldType? parsed = text.ToLowerInvariant() switch
        {
            "uint8" or "u8" => FieldType.UInt8,
            "uint16" or "u16" => FieldType.UInt16,
            "uint32" or "u32" => FieldType.UInt32,
            "uint64" or "u64" => FieldType.UInt64,
            "int8" or "i8" => FieldType.Int8,
            "int16" or "i16" => FieldType.Int16,
            "int32" or "i32" => FieldType.Int32,
            "int64" or "i64" => FieldType.Int64,
            "bytes" => FieldType.Bytes,
            "ascii" => FieldType.Ascii,
            "utf8" => FieldType.Utf8,
            "utf16le" or "utf16-le" => FieldType.Utf16Le,
            "utf16be" or "utf16-be" => FieldType.Utf16Be,
            "sjis" or "shift_jis" or "shift-jis" => FieldType.ShiftJis,
            "latin1" or "iso-8859-1" => FieldType.Latin1,
            "asciiz" => FieldType.AsciiZ,
            "utf8z" => FieldType.Utf8Z,
            "float16" or "f16" => FieldType.Float16,
            "float32" or "f32" => FieldType.Float32,
            "float64" or "f64" => FieldType.Float64,
            "struct" => FieldType.Struct,
            "switch" => FieldType.Switch,
            "bitfield" => FieldType.Bitfield,
            "zlib" => FieldType.Zlib,
            "deflate" => FieldType.Deflate,
            "gzip" => FieldType.Gzip,
            "bzip2" => FieldType.Bzip2,
            "lzma" => FieldType.Lzma,
            "zstd" or "zstandard" => FieldType.Zstd,
            "lz4" => FieldType.Lz4,
            "virtual" => FieldType.Virtual,
            "uleb128" or "leb128u" => FieldType.ULeb128,
            "sleb128" or "leb128s" => FieldType.SLeb128,
            "vlq" => FieldType.Vlq,
            _ => null,
        };
        type = parsed ?? default;
        return parsed.HasValue;
    }
}
