namespace BinAnalyzer.Integration.Tests;

public static class MsgpackTestDataGenerator
{
    /// <summary>
    /// 最小MessagePackファイル (6 bytes):
    /// fixmap(1 entry) + fixstr("key") + positive fixint(42)
    /// 81 a3 6b 65 79 2a
    /// </summary>
    public static byte[] CreateMinimalMsgpack()
    {
        return new byte[]
        {
            0x81,             // fixmap with 1 entry (0x80 | 0x01)
            0xa3,             // fixstr with length 3 (0xa0 | 0x03)
            0x6b, 0x65, 0x79, // "key" in UTF-8
            0x2a              // positive fixint 42 (0x00-0x7f range)
        };
    }
}
