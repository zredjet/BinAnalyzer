using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class TarTestDataGenerator
{
    /// <summary>
    /// 最小TARファイル: 512バイトブロック1つ（UStarヘッダのみ、ファイルサイズ0の通常ファイル）
    /// name + mode + uid + gid + size + mtime + checksum + typeflag + linkname + magic + version +
    /// uname + gname + devmajor + devminor + prefix + padding = 512B
    /// </summary>
    public static byte[] CreateMinimalTar()
    {
        var data = new byte[512];

        // name: "hello.txt" (100 bytes)
        WriteAscii(data, 0, "hello.txt", 100);

        // mode: "0000644" (8 bytes)
        WriteAscii(data, 100, "0000644\0", 8);

        // uid: "0001000" (8 bytes)
        WriteAscii(data, 108, "0001000\0", 8);

        // gid: "0001000" (8 bytes)
        WriteAscii(data, 116, "0001000\0", 8);

        // size: "00000000000" (12 bytes) - file size = 0
        WriteAscii(data, 124, "00000000000\0", 12);

        // mtime: "14246320600" (12 bytes)
        WriteAscii(data, 136, "14246320600\0", 12);

        // checksum placeholder: 8 spaces (will be computed)
        for (var i = 148; i < 156; i++) data[i] = 0x20;

        // typeflag: '0' (regular file)
        data[156] = 0x30;

        // linkname: empty (100 bytes) - already zeroed

        // magic: "ustar\0" (6 bytes)
        WriteAscii(data, 257, "ustar\0", 6);

        // version: "00" (2 bytes)
        WriteAscii(data, 263, "00", 2);

        // uname: "user" (32 bytes)
        WriteAscii(data, 265, "user", 32);

        // gname: "group" (32 bytes)
        WriteAscii(data, 297, "group", 32);

        // devmajor, devminor, prefix, header_padding: already zeroed

        // Compute checksum: sum of all bytes (checksum field treated as spaces)
        var checksum = 0;
        for (var i = 0; i < 512; i++)
            checksum += data[i];

        // Write checksum as octal ASCII (6 digits + NUL + space)
        var checksumStr = Convert.ToString(checksum, 8).PadLeft(6, '0');
        WriteAscii(data, 148, checksumStr + "\0 ", 8);

        return data;
    }

    private static void WriteAscii(byte[] data, int offset, string value, int fieldSize)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        Array.Copy(bytes, 0, data, offset, Math.Min(bytes.Length, fieldSize));
    }
}
