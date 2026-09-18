namespace BinAnalyzer.Fuzz.Tests;

/// <summary>ランダム入力と、有効なサンプルからの変異入力。</summary>
public static class InputGenerator
{
    /// <summary>境界値として混ぜる「面白い」バイト列（0 / 最大値 / 符号境界 / 巨大な長さ）。</summary>
    private static readonly byte[][] Interesting =
    [
        [0x00], [0xFF], [0x7F], [0x80], [0x01],
        [0x00, 0x00], [0xFF, 0xFF], [0x7F, 0xFF], [0x80, 0x00],
        [0x00, 0x00, 0x00, 0x00], [0xFF, 0xFF, 0xFF, 0xFF], [0x7F, 0xFF, 0xFF, 0xFF], [0x80, 0x00, 0x00, 0x00],
        [0xFF, 0xFF, 0xFF, 0x7F], [0x00, 0x00, 0x00, 0x80],
        [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F],
    ];

    /// <summary>純ランダム。長さは 0〜<paramref name="maxLength"/>（小さい長さを多めに）。</summary>
    public static byte[] Random(Random rng, int maxLength = 4096)
    {
        var length = rng.Next(4) switch
        {
            0 => rng.Next(0, 16),
            1 => rng.Next(0, 256),
            _ => rng.Next(0, maxLength + 1),
        };
        var data = new byte[length];
        rng.NextBytes(data);
        return data;
    }

    /// <summary>ランダムだが 0x00 / 0xFF に偏らせる（長さ 0・最大値の経路を踏みやすい）。</summary>
    public static byte[] Skewed(Random rng, int maxLength = 4096)
    {
        var data = Random(rng, maxLength);
        var fill = rng.Next(3) switch { 0 => (byte)0x00, 1 => (byte)0xFF, _ => (byte)rng.Next(256) };
        for (var i = 0; i < data.Length; i++)
            if (rng.Next(3) == 0) data[i] = fill;
        return data;
    }

    /// <summary>有効なサンプルに 1 種類の変異を加える。</summary>
    public static (byte[] Data, string Description) Mutate(Random rng, byte[] sample)
    {
        if (sample.Length == 0)
            return (Random(rng), "random (empty sample)");
        var data = (byte[])sample.Clone();
        switch (rng.Next(9))
        {
            case 0:
            {
                var n = rng.Next(1, 5);
                var at = new List<int>();
                for (var i = 0; i < n; i++) { var p = rng.Next(data.Length); data[p] = (byte)rng.Next(256); at.Add(p); }
                return (data, $"set {n} random bytes at [{string.Join(",", at)}]");
            }
            case 1:
            {
                var p = rng.Next(data.Length);
                var bit = rng.Next(8);
                data[p] ^= (byte)(1 << bit);
                return (data, $"flip bit {bit} at {p}");
            }
            case 2:
            {
                var v = Interesting[rng.Next(Interesting.Length)];
                var p = rng.Next(Math.Max(1, data.Length - v.Length + 1));
                var len = Math.Min(v.Length, data.Length - p);
                Array.Copy(v, 0, data, p, len);
                return (data, $"interesting {Convert.ToHexString(v)} at {p}");
            }
            case 3:
            {
                var p = rng.Next(1, data.Length + 1);
                return (data[..p], $"truncate to {p}");
            }
            case 4:
            {
                var extra = new byte[rng.Next(1, 64)];
                rng.NextBytes(extra);
                return ([.. data, .. extra], $"append {extra.Length} random bytes");
            }
            case 5:
            {
                var start = rng.Next(data.Length);
                var len = rng.Next(1, Math.Min(64, data.Length - start) + 1);
                var chunk = data[start..(start + len)];
                var at = rng.Next(data.Length + 1);
                return ([.. data[..at], .. chunk, .. data[at..]], $"duplicate {len} bytes from {start} at {at}");
            }
            case 6:
            {
                var start = rng.Next(data.Length);
                var len = rng.Next(1, Math.Min(64, data.Length - start) + 1);
                return ([.. data[..start], .. data[(start + len)..]], $"delete {len} bytes at {start}");
            }
            case 7:
            {
                var start = rng.Next(data.Length);
                var len = rng.Next(1, Math.Min(16, data.Length - start) + 1);
                var fill = rng.Next(2) == 0 ? (byte)0x00 : (byte)0xFF;
                Array.Fill(data, fill, start, len);
                return (data, $"fill {len} bytes with {fill:X2} at {start}");
            }
            default:
            {
                var p = rng.Next(data.Length);
                var q = rng.Next(data.Length);
                (data[p], data[q]) = (data[q], data[p]);
                return (data, $"swap {p} and {q}");
            }
        }
    }
}
