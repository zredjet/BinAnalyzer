using System.Text;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Diff;

namespace BinAnalyzer.Output;

/// <summary>
/// 2 つのバイナリのバイト単位の差分を hexdump 形式で出す（<c>diff --output hexdump</c>、REQ-156）。
/// 同じオフセット同士を比べ、差分のある行は <c>-</c>（file1）/ <c>+</c>（file2）の 2 行、同じ行は 1 行で出す。
/// 色が無効なときは差分バイトの下に <c>^^</c> の行を添える。差分行の右端には、その行の最初の差分バイトが属するフィールドのパス。
/// </summary>
public sealed class HexDiffOutputFormatter
{
    private const int BytesPerRow = 16;
    private readonly bool _useColor;

    /// <summary>直近の <see cref="Format"/> で差分があったか。</summary>
    public bool HasDifferences { get; private set; }

    public HexDiffOutputFormatter(ColorMode mode = ColorMode.Never)
    {
        _useColor = mode switch
        {
            ColorMode.Always => true,
            ColorMode.Never => false,
            ColorMode.Auto => !Console.IsOutputRedirected,
            _ => false,
        };
    }

    private string C(string text, string color)
        => _useColor ? $"{color}{text}{AnsiColors.Reset}" : text;

    /// <param name="leftRoot">file1 のデコード結果（差分行のフィールド名に使う。無ければ出さない）。</param>
    /// <param name="rightRoot">file2 のデコード結果（file1 に無い範囲のフィールド名に使う）。</param>
    /// <param name="onlyDiff">true なら差分のある行だけを出し、飛ばした行は <c>...</c> で示す。</param>
    public string Format(ReadOnlyMemory<byte> left, DecodedStruct? leftRoot, ReadOnlyMemory<byte> right, DecodedStruct? rightRoot, bool onlyDiff)
    {
        var leftFields = Fields(leftRoot);
        var rightFields = Fields(rightRoot);
        var l = left.Span;
        var r = right.Span;
        var length = Math.Max(l.Length, r.Length);
        var rows = (length + BytesPerRow - 1) / BytesPerRow;

        var sb = new StringBuilder();
        sb.AppendLine(C("  Offset    00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F  ASCII             Field", AnsiColors.Dim));
        sb.AppendLine(C("  ────────  ─────────────────────────────────────────────────  ────────────────  ─────────────────────", AnsiColors.Dim));

        var diffRows = 0;
        var skipped = false;
        for (var row = 0; row < rows; row++)
        {
            long rowStart = (long)row * BytesPerRow;
            if (!ByteDiff.RowDiffers(l, r, rowStart, BytesPerRow))
            {
                if (onlyDiff)
                {
                    skipped = true;
                    continue;
                }
                AppendLine(sb, ' ', l, rowStart, 0, null, "");
                continue;
            }

            if (skipped)
            {
                sb.AppendLine(C("  ...", AnsiColors.Dim));
                skipped = false;
            }
            diffRows++;
            var mask = ByteDiff.RowMask(l, r, rowStart, BytesPerRow);
            var first = rowStart + System.Numerics.BitOperations.TrailingZeroCount(mask);
            var field = FieldAt(leftFields, first) ?? FieldAt(rightFields, first) ?? "";
            AppendLine(sb, '-', l, rowStart, mask, AnsiColors.Red, field);
            AppendLine(sb, '+', r, rowStart, mask, AnsiColors.Green, "");
            if (!_useColor)
                AppendCarets(sb, mask);
        }
        if (skipped)
            sb.AppendLine(C("  ...", AnsiColors.Dim));

        var bytes = ByteDiff.CountDifferences(l, r);
        HasDifferences = bytes > 0;
        sb.AppendLine();
        sb.AppendLine(HasDifferences
            ? $"差分: {bytes} バイト（{diffRows} 行）  file1 {l.Length} バイト / file2 {r.Length} バイト"
            : $"差分なし（{l.Length} バイト）");
        return sb.ToString();
    }

    /// <summary>1 行。<paramref name="mask"/> の立ったバイトを <paramref name="diffColor"/> で強調する。行にバイトが無い列は空白。</summary>
    private void AppendLine(StringBuilder sb, char sign, ReadOnlySpan<byte> data, long rowStart, uint mask, string? diffColor, string field)
    {
        sb.Append(sign == ' ' ? "  " : C($"{sign} ", diffColor!));
        sb.Append(C(rowStart.ToString("X8"), AnsiColors.Dim));
        sb.Append("  ");

        var hex = new StringBuilder();
        var ascii = new StringBuilder();
        for (var col = 0; col < BytesPerRow; col++)
        {
            if (col == 8) hex.Append(' ');
            var off = rowStart + col;
            if (off >= data.Length)
            {
                hex.Append("   ");
                ascii.Append(' ');
                continue;
            }
            var b = data[(int)off];
            var differs = (mask & (1u << col)) != 0;
            var h = b.ToString("X2");
            var a = (b is >= 0x20 and <= 0x7E ? (char)b : '.').ToString();
            hex.Append(differs ? C(h, diffColor!) : h).Append(' ');
            ascii.Append(differs ? C(a, diffColor!) : a);
        }
        var line = new StringBuilder().Append(hex).Append(' ').Append(ascii);
        if (field.Length > 0)
            line.Append("  ").Append(C(field, AnsiColors.Cyan));
        sb.AppendLine(line.ToString().TrimEnd());
    }

    /// <summary>色が無効なとき、差分バイトの位置に ^^ を出す行。</summary>
    private static void AppendCarets(StringBuilder sb, uint mask)
    {
        var line = new StringBuilder("            ");
        for (var col = 0; col < BytesPerRow; col++)
        {
            if (col == 8) line.Append(' ');
            line.Append((mask & (1u << col)) != 0 ? "^^ " : "   ");
        }
        sb.AppendLine(line.ToString().TrimEnd());
    }

    private static List<HexDumpOutputFormatter.FieldRegion> Fields(DecodedStruct? root)
    {
        var fields = new List<HexDumpOutputFormatter.FieldRegion>();
        if (root is not null)
        {
            HexDumpOutputFormatter.CollectLeafFields(root, "", fields);
            fields.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        }
        return fields;
    }

    /// <summary>offset を含む葉フィールドのパス（Offset 順に並んだ一覧を二分探索）。</summary>
    private static string? FieldAt(List<HexDumpOutputFormatter.FieldRegion> fields, long offset)
    {
        int lo = 0, hi = fields.Count - 1, found = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (fields[mid].Offset <= offset) { found = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        for (var i = found; i >= 0 && i > found - 4; i--)
        {
            var f = fields[i];
            if (offset >= f.Offset && offset < f.Offset + f.Size)
                return f.Path;
        }
        return null;
    }
}
