namespace BinAnalyzer.Engine;

/// <summary>
/// Fletcher-16 / Fletcher-32 チェックサムを計算する。
/// Fletcher-16: 8-bit ワード入力、16-bit 出力
/// Fletcher-32: 16-bit ワード入力、32-bit 出力
/// </summary>
public static class FletcherCalculator
{
    /// <summary>
    /// Fletcher-16 を計算する（8-bit ワード入力）。
    /// テストベクター: "abcde" -> 0xC8F0
    /// </summary>
    public static ushort ComputeFletcher16(ReadOnlySpan<byte> data)
    {
        ushort sum1 = 0;
        ushort sum2 = 0;
        foreach (var b in data)
        {
            sum1 = (ushort)((sum1 + b) % 255);
            sum2 = (ushort)((sum2 + sum1) % 255);
        }
        return (ushort)((sum2 << 8) | sum1);
    }

    /// <summary>
    /// Fletcher-32 を計算する（16-bit ワード入力、ビッグエンディアン）。
    /// 奇数バイト長はゼロパディング。
    /// </summary>
    public static uint ComputeFletcher32(ReadOnlySpan<byte> data)
    {
        uint sum1 = 0;
        uint sum2 = 0;
        var wordCount = (data.Length + 1) / 2;
        for (int i = 0; i < wordCount; i++)
        {
            var offset = i * 2;
            ushort word;
            if (offset + 1 < data.Length)
            {
                word = (ushort)((data[offset] << 8) | data[offset + 1]);
            }
            else
            {
                // 奇数バイト: 最後のバイトをゼロパディング
                word = (ushort)(data[offset] << 8);
            }
            sum1 = (sum1 + word) % 65535;
            sum2 = (sum2 + sum1) % 65535;
        }
        return (sum2 << 16) | sum1;
    }
}
