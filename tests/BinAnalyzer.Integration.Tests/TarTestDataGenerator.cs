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

    /// <summary>
    /// pax 拡張ヘッダ（path）+ ustar のファイル、GNU の長い名前（'L'）+ GNU 形式のファイル、終端の 0 ブロック 2 個と
    /// 10240 バイトまでの詰め物からなる tar（REQ-188）。
    /// </summary>
    public static byte[] CreatePaxAndGnuTar()
    {
        var ms = new MemoryStream();
        const string longPath = "dir/a-very-long-file-name-that-is-stored-in-a-pax-record.txt";
        const string gnuName = "gnu/another-long-name-for-the-gnu-longlink-entry.txt";

        var paxBody = PaxRecord("path", longPath) + PaxRecord("mtime", "1700000000.5");
        WriteEntry(ms, "PaxHeader/long", 'x', Encoding.UTF8.GetBytes(paxBody), gnu: false);
        WriteEntry(ms, "long.txt", '0', Encoding.ASCII.GetBytes("hi\n"), gnu: false);
        WriteEntry(ms, "././@LongLink", 'L', Encoding.ASCII.GetBytes(gnuName + "\0"), gnu: true);
        WriteEntry(ms, gnuName[..50], '0', Encoding.ASCII.GetBytes("gnu"), gnu: true);

        ms.Write(new byte[1024]);
        ms.Write(new byte[10240 - ms.Length % 10240]);
        return ms.ToArray();
    }

    /// <summary>pax のレコード「長さ キーワード=値\n」（長さはレコード全体のバイト数で、長さ自身の桁数を含む）。</summary>
    private static string PaxRecord(string keyword, string value)
    {
        var rest = $" {keyword}={value}\n";
        var length = rest.Length + 1;
        while ($"{length}{rest}".Length != length)
            length++;
        return $"{length}{rest}";
    }

    private static void WriteEntry(MemoryStream ms, string name, char typeflag, byte[] content, bool gnu)
    {
        var header = new byte[512];
        WriteAscii(header, 0, name, 100);
        WriteAscii(header, 100, "0000644", 8);
        WriteAscii(header, 108, "0001750", 8);
        WriteAscii(header, 116, "0001750", 8);
        WriteAscii(header, 124, Convert.ToString(content.Length, 8).PadLeft(11, '0'), 12);
        WriteAscii(header, 136, Convert.ToString(1_700_000_000, 8).PadLeft(11, '0'), 12);
        header[156] = (byte)typeflag;
        if (gnu)
        {
            WriteAscii(header, 257, "ustar ", 6);
            WriteAscii(header, 263, " ", 2);
        }
        else
        {
            WriteAscii(header, 257, "ustar", 6);
            WriteAscii(header, 263, "00", 2);
        }
        WriteAscii(header, 265, "user", 32);
        WriteAscii(header, 297, "group", 32);
        // チェックサム: chksum を空白 8 個とみなした 512 バイトの合計（6 桁の 8 進数 + NUL + 空白）
        Array.Fill(header, (byte)' ', 148, 8);
        var sum = header.Sum(b => b);
        WriteAscii(header, 148, Convert.ToString(sum, 8).PadLeft(6, '0'), 7);
        header[155] = (byte)' ';

        ms.Write(header);
        ms.Write(content);
        ms.Write(new byte[(512 - content.Length % 512) % 512]);
    }
}
