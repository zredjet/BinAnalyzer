using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Patching;

namespace BinAnalyzer.Engine;

/// <summary>
/// デコード済みノードのメタデータ（<see cref="DecodedNode.DslType"/>・サイズ・エンディアン・エンコーディング）に従って
/// 入力文字列を同じ長さのバイト列にする。サイズが変わる編集は受け付けない。
/// </summary>
public sealed class FieldEncoder : IFieldEncoder
{
    public static readonly FieldEncoder Instance = new();

    public bool CanEncode(DecodedNode node, out string? reason)
        => FieldEditRules.Classify(node, out reason) != EditKind.None;

    public FieldEncodeResult Encode(DecodedNode node, string input)
    {
        var kind = FieldEditRules.Classify(node, out var reason);
        return kind switch
        {
            EditKind.Integer or EditKind.Enum => EncodeInteger((DecodedInteger)node, input),
            EditKind.Float => EncodeFloat((DecodedFloat)node, input),
            EditKind.String => EncodeString((DecodedString)node, input),
            EditKind.Bytes => EncodeBytes((DecodedBytes)node, input),
            _ => FieldEncodeResult.Fail(reason ?? "編集できないフィールドです"),
        };
    }

    public string InitialText(DecodedNode node) => node switch
    {
        DecodedInteger { DslType: FieldType.UInt64 } i => ((ulong)i.Value).ToString(CultureInfo.InvariantCulture),
        DecodedInteger i => i.Value.ToString(CultureInfo.InvariantCulture),
        DecodedFloat f => f.Value.ToString("R", CultureInfo.InvariantCulture),
        DecodedString s => s.Value.TrimEnd('\0'),
        DecodedBytes b => FormatHex(b.RawBytes.Span),
        _ => "",
    };

    /// <summary>バイト列を <c>89 50 4E 47</c> 形式にする。</summary>
    public static string FormatHex(ReadOnlySpan<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length * 3);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    // ---- integer ----

    private static FieldEncodeResult EncodeInteger(DecodedInteger node, string input)
    {
        var text = input.Trim();
        if (text.Length == 0)
            return FieldEncodeResult.Fail("値を入力してください");
        if (!TryParseInteger(text, out var value))
            return FieldEncodeResult.Fail($"整数として解釈できません: {text}");

        var type = node.DslType!.Value;
        var size = (int)node.Size;
        var bits = size * 8;
        var signed = FieldEditRules.IsSignedInteger(type);
        var min = signed ? -(Int128.One << (bits - 1)) : Int128.Zero;
        var max = signed ? (Int128.One << (bits - 1)) - 1 : (Int128.One << bits) - 1;
        if (value < min || value > max)
            return FieldEncodeResult.Fail($"{TypeName(type)} の範囲外です（{min} ～ {max}）");

        var raw = (ulong)(value & ((Int128.One << bits) - 1));
        var bytes = new byte[size];
        var endian = node.Endianness ?? Endianness.Big;
        for (var i = 0; i < size; i++)
        {
            var b = (byte)(raw >> (8 * i));
            if (endian == Endianness.Little) bytes[i] = b; else bytes[size - 1 - i] = b;
        }
        return FieldEncodeResult.Ok(bytes);
    }

    /// <summary>10 進、または <c>0x</c> 接頭辞の 16 進（負号は先頭）。</summary>
    internal static bool TryParseInteger(string text, out Int128 value)
    {
        value = default;
        var negative = false;
        var s = text;
        if (s.StartsWith('-') || s.StartsWith('+'))
        {
            negative = s[0] == '-';
            s = s[1..];
        }
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            var hex = s[2..];
            if (hex.Length == 0 || hex.Length > 16)
                return false;
            if (!UInt128.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var u))
                return false;
            value = (Int128)u;
        }
        else
        {
            if (!Int128.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value))
                return false;
        }
        if (negative) value = -value;
        return true;
    }

    private static string TypeName(FieldType type) => type.ToString().ToLowerInvariant();

    // ---- float ----

    private static FieldEncodeResult EncodeFloat(DecodedFloat node, string input)
    {
        var text = input.Trim();
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return FieldEncodeResult.Fail($"数値として解釈できません: {text}");

        var endian = node.Endianness ?? Endianness.Big;
        if (node.IsSinglePrecision)
        {
            var single = (float)value;
            if (float.IsInfinity(single) && !double.IsInfinity(value))
                return FieldEncodeResult.Fail("float32 の範囲外です");
            var bytes = new byte[4];
            if (endian == Endianness.Big) BinaryPrimitives.WriteSingleBigEndian(bytes, single);
            else BinaryPrimitives.WriteSingleLittleEndian(bytes, single);
            return FieldEncodeResult.Ok(bytes);
        }
        else
        {
            var bytes = new byte[8];
            if (endian == Endianness.Big) BinaryPrimitives.WriteDoubleBigEndian(bytes, value);
            else BinaryPrimitives.WriteDoubleLittleEndian(bytes, value);
            return FieldEncodeResult.Ok(bytes);
        }
    }

    // ---- string ----

    private static FieldEncodeResult EncodeString(DecodedString node, string input)
    {
        var encoding = ResolveEncoding(node.Encoding);
        if (encoding is null)
            return FieldEncodeResult.Fail($"エンコーディング {node.Encoding} は編集に対応していません");

        if (encoding == Encoding.ASCII && input.Any(c => c > 0x7F))
            return FieldEncodeResult.Fail("ASCII 以外の文字が含まれています");
        if (encoding == Encoding.Latin1 && input.Any(c => c > 0xFF))
            return FieldEncodeResult.Fail("Latin-1 で表せない文字が含まれています");

        var encoded = encoding.GetBytes(input);
        var size = (int)node.Size;
        if (encoded.Length > size)
            return FieldEncodeResult.Fail($"{size} バイトを超えています（{encoded.Length} バイト）");

        if (encoded.Length == size)
            return FieldEncodeResult.Ok(encoded);

        var bytes = new byte[size];
        encoded.CopyTo(bytes, 0);
        return FieldEncodeResult.Ok(bytes, $"不足分 {size - encoded.Length} バイトを 0x00 で埋めます");
    }

    private static Encoding? ResolveEncoding(string name) => name.ToLowerInvariant() switch
    {
        "ascii" => Encoding.ASCII,
        "utf8" or "utf-8" => Encoding.UTF8,
        "utf16le" => Encoding.Unicode,
        "utf16be" => Encoding.BigEndianUnicode,
        "sjis" or "shift_jis" => EncodingHelper.ShiftJis,
        "latin1" => Encoding.Latin1,
        _ => null,
    };

    // ---- bytes ----

    private static FieldEncodeResult EncodeBytes(DecodedBytes node, string input)
    {
        var digits = new StringBuilder();
        var s = input.Trim();
        // "0x" 接頭辞や区切り文字（空白・カンマ・コロン・ハイフン）は読み飛ばす
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c) || c is ',' or ':' or '-')
                continue;
            if (c == '0' && i + 1 < s.Length && (s[i + 1] == 'x' || s[i + 1] == 'X'))
            {
                i++;
                continue;
            }
            if (!Uri.IsHexDigit(c))
                return FieldEncodeResult.Fail($"16 進数として解釈できません: '{c}'");
            digits.Append(c);
        }
        if (digits.Length % 2 != 0)
            return FieldEncodeResult.Fail("16 進数の桁数が奇数です");

        var size = (int)node.Size;
        var count = digits.Length / 2;
        if (count != size)
            return FieldEncodeResult.Fail($"{size} バイト必要です（{count} バイト入力）");

        return FieldEncodeResult.Ok(Convert.FromHexString(digits.ToString()));
    }
}
