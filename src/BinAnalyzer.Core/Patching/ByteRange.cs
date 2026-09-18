namespace BinAnalyzer.Core.Patching;

/// <summary>ファイル（またはストリーム）上の連続したバイト範囲 [Offset, End)。</summary>
public readonly record struct ByteRange(long Offset, long Size)
{
    public long End => Offset + Size;

    public bool IsEmpty => Size <= 0;

    public bool Contains(long offset) => offset >= Offset && offset < End;

    /// <summary>範囲が 1 バイト以上重なるか。空範囲は何とも重ならない。</summary>
    public bool Overlaps(ByteRange other)
        => !IsEmpty && !other.IsEmpty && Offset < other.End && other.Offset < End;
}
