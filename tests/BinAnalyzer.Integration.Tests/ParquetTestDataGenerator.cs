using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class ParquetTestDataGenerator
{
    /// <summary>
    /// 最小Parquetバイナリ: magic(4B) + row_groups(8B) = 12バイト
    /// 末尾にはフッタサイズ(4B) + "PAR1"(4B)のダミーデータを含む
    /// </summary>
    public static byte[] CreateMinimalParquet()
    {
        using var ms = new MemoryStream();

        // magic: "PAR1"
        ms.Write(Encoding.ASCII.GetBytes("PAR1"));

        // row_groups: remaining (dummy thrift data + footer size + PAR1)
        ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // dummy footer length = 0
        ms.Write(Encoding.ASCII.GetBytes("PAR1"));        // trailing magic

        return ms.ToArray();
    }
}
