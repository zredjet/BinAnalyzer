namespace BinAnalyzer.Core.Diff;

/// <summary>
/// 2 つのバイト列の同じオフセット同士を比べる（REQ-156）。差分範囲を事前に列挙せず、表示・出力する行ごとにその場で比べる。
/// 長さが違う場合、短い側の末尾を越えるオフセットは「差分あり」とする。
/// </summary>
public static class ByteDiff
{
    /// <summary><paramref name="offset"/> のバイトが異なるか（片方にしか無ければ true、両方に無ければ false）。</summary>
    public static bool Differs(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, long offset)
    {
        var inLeft = offset < left.Length;
        var inRight = offset < right.Length;
        if (inLeft && inRight)
            return left[(int)offset] != right[(int)offset];
        return inLeft != inRight;
    }

    /// <summary>
    /// <paramref name="rowStart"/> から <paramref name="rowLength"/> バイトのうち、異なるバイトのビット（bit i = rowStart + i）。
    /// <paramref name="rowLength"/> は 32 以下。
    /// </summary>
    public static uint RowMask(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, long rowStart, int rowLength)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rowLength, 32);
        uint mask = 0;
        for (var i = 0; i < rowLength; i++)
        {
            if (Differs(left, right, rowStart + i))
                mask |= 1u << i;
        }
        return mask;
    }

    /// <summary><paramref name="rowStart"/> から <paramref name="rowLength"/> バイトに異なるバイトがあるか。</summary>
    public static bool RowDiffers(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, long rowStart, int rowLength)
    {
        var end = rowStart + rowLength;
        var commonEnd = Math.Min(end, Math.Min(left.Length, right.Length));
        if (rowStart < commonEnd
            && !left[(int)rowStart..(int)commonEnd].SequenceEqual(right[(int)rowStart..(int)commonEnd]))
            return true;
        // 共通部分を越えた範囲は、片方にだけバイトがあれば差分
        var longer = Math.Max(left.Length, right.Length);
        return Math.Max(rowStart, commonEnd) < Math.Min(end, longer);
    }

    /// <summary>異なるバイトの数（共通の長さでの不一致 + 長さの差）。一致する区間は読み飛ばす。</summary>
    public static long CountDifferences(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var common = Math.Min(left.Length, right.Length);
        long count = Math.Abs(left.Length - right.Length);
        var i = 0;
        while (i < common)
        {
            i += left[i..common].CommonPrefixLength(right[i..common]);
            if (i >= common)
                break;
            count++;
            i++;
        }
        return count;
    }
}
