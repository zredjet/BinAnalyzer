using System.Buffers.Binary;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class GifTestDataGenerator
{
    /// <summary>
    /// 最小GIFファイル(グローバルカラーテーブルなし): signature(3B) + version(3B) +
    /// logical_screen_desc(7B) + trailer block(2B) = 15バイト
    /// GIF89a, 1x1, global_color_table_flag=0
    /// blocks配列にtrailer(0x3B)を1つ含む
    /// </summary>
    public static byte[] CreateMinimalGif()
    {
        using var ms = new MemoryStream();

        // signature: "GIF"
        ms.Write(Encoding.ASCII.GetBytes("GIF"));

        // version: "89a"
        ms.Write(Encoding.ASCII.GetBytes("89a"));

        // === logical_screen_desc ===
        // width: 1 (uint16 LE)
        var buf = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, 1);
        ms.Write(buf);

        // height: 1 (uint16 LE)
        BinaryPrimitives.WriteUInt16LittleEndian(buf, 1);
        ms.Write(buf);

        // packed: global_color_table_flag=0, color_resolution=0, sort_flag=0, size_of_global_color_table=0
        ms.WriteByte(0x00);

        // bg_color_index: 0
        ms.WriteByte(0x00);

        // pixel_aspect_ratio: 0
        ms.WriteByte(0x00);

        // === blocks: trailer ===
        // introducer: 0x3B (Trailer)
        ms.WriteByte(0x3B);

        // body: switch -> trailer (empty struct, no additional bytes consumed)
        // The trailer case consumes 0 bytes

        return ms.ToArray();
    }
}
