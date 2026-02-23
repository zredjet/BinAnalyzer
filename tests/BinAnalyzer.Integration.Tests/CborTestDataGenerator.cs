namespace BinAnalyzer.Integration.Tests;

public static class CborTestDataGenerator
{
    /// <summary>
    /// 最小CBORファイル (7 bytes):
    /// map(1 pair) + text(3, "key") + unsigned int(24, 42)
    /// a1 63 6b 65 79 18 2a
    ///
    /// Breakdown:
    /// a1       = major type 5 (map), additional info 1 → map with 1 pair
    /// 63       = major type 3 (text string), additional info 3 → 3-byte text
    /// 6b 65 79 = "key" in UTF-8
    /// 18       = major type 0 (unsigned int), additional info 24 → next byte is value
    /// 2a       = 42
    /// </summary>
    public static byte[] CreateMinimalCbor()
    {
        return new byte[]
        {
            0xa1,             // map(1) - major type 5, additional info 1
            0x63,             // text(3) - major type 3, additional info 3
            0x6b, 0x65, 0x79, // "key" in UTF-8
            0x18,             // unsigned int, additional info 24 (next 1 byte)
            0x2a              // value: 42
        };
    }
}
