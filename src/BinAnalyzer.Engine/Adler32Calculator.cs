namespace BinAnalyzer.Engine;

/// <summary>
/// Adler-32 チェックサムを計算する（RFC 1950 準拠）。
/// zlib ストリームのチェックサム検証に使用される。
/// テストベクター: "Wikipedia" → 0x11E60398
/// </summary>
public static class Adler32Calculator
{
    private const uint ModAdler = 65521;

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint a = 1;
        uint b = 0;

        foreach (var d in data)
        {
            a = (a + d) % ModAdler;
            b = (b + a) % ModAdler;
        }

        return (b << 16) | a;
    }
}
