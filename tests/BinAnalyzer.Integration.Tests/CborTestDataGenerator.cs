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

    /// <summary>
    /// RFC 8949 付録 A の例を並べた CBOR シーケンス（REQ-188）。整数（1〜8 バイトの引数・負の数）、半精度・単精度・倍精度の浮動小数点数、
    /// 単純値、タグ（0 / 1 / 23 / 24 / 32）、バイト列・文字列（UTF-8）、配列・マップ、不定長のバイト列・文字列・配列・マップ（入れ子を含む）。
    /// </summary>
    public static byte[] CreateCborRfc8949Examples() => Convert.FromHexString(
        "00" + "17" + "1818" + "1903e8" + "1a000f4240" + "1b000000e8d4a51000" + "20" + "3903e7" +
        "f93c00" + "f9c400" + "fa47c35000" + "fb3ff199999999999a" +
        "f4" + "f5" + "f6" + "f7" + "f818" +
        "c074323031332d30332d32315432303a30343a30305a" + "c11a514b67b0" + "d74401020304" + "d818456449455446" +
        "d82076687474703a2f2f7777772e6578616d706c652e636f6d" +
        "4401020304" + "6449455446" + "62c3bc" + "64f0908591" +
        "8301820203820405" + "a26161016162820203" +
        "5f42010243030405ff" + "7f657374726561646d696e67ff" + "9f018202039f0405ffff" + "83018202039f0405ff" + "bf61610161629f0203ffff");
}
