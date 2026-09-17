namespace BinAnalyzer.Core.Patching;

/// <summary>バイト列の一部を同じ長さで置き換える書き込み。サイズを変える編集は表現しない。</summary>
public sealed record BytePatch(long Offset, byte[] Bytes)
{
    public ByteRange Range => new(Offset, Bytes.Length);
}
