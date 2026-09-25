using System.Globalization;

namespace BinAnalyzer.Core.Models;

/// <summary>
/// 整数の値の表記（REQ-201）。デコード結果と式の整数は符号付き 64 ビット（long）で持つので、
/// 符号なし 64 ビットの型（uint64・uleb128・vlq）の 2^63 以上の値は、同じビットの負の long になっている。
/// 表示するときはここを通して符号なしの 10 進に戻す。
/// </summary>
public static class IntegerText
{
    /// <summary>符号なし 64 ビットとして表示する型（uint64・uleb128・vlq）。</summary>
    public static bool IsUnsigned64(FieldType? type) =>
        type is FieldType.UInt64 or FieldType.ULeb128 or FieldType.Vlq;

    /// <summary>型に合わせた 10 進の表記（インバリアントカルチャ）。符号なし 64 ビットの型は <c>(ulong)value</c>。</summary>
    public static string Format(long value, FieldType? type) =>
        IsUnsigned64(type)
            ? ((ulong)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);

    /// <summary>16 進を添えるか（絶対値が 16 以上）。符号なし 64 ビットの型は 2^63 以上も大きな値として扱う。</summary>
    public static bool ShowsHex(long value, FieldType? type) =>
        IsUnsigned64(type) ? (ulong)value >= 16 : value is >= 16 or <= -16;

    /// <summary>ビットの並びの値（bitfield のエントリ）の 10 進の表記。エントリは常に符号なしなので、64 ビットの幅の上位ビットも正の値にする。</summary>
    public static string FormatBits(long value) => ((ulong)value).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// DSL の整数のリテラル（enum の値など）を読む。10 進（負号可。-2^63 〜 2^64 - 1）と <c>0x</c> の 16 進（16 桁まで）。
    /// 2^63 以上の値は同じビットの負の long にする（uint64 のフィールドの値と比べられるように）。
    /// </summary>
    public static bool TryParseLiteral(string? text, out long value)
    {
        value = 0;
        var s = text?.Trim().Replace("_", "");
        if (string.IsNullOrEmpty(s))
            return false;
        var negative = s[0] == '-';
        if (s[0] is '-' or '+')
            s = s[1..];
        ulong magnitude;
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!ulong.TryParse(s.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out magnitude))
                return false;
        }
        else if (!ulong.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out magnitude))
            return false;

        if (!negative)
        {
            value = unchecked((long)magnitude);
            return true;
        }
        if (magnitude > (ulong)long.MaxValue + 1)
            return false;
        value = unchecked(-(long)magnitude);
        return true;
    }
}
