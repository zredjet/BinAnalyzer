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

    /// <summary>
    /// イメージブロック付きGIF: signature(3B) + version(3B) +
    /// logical_screen_desc(7B) + image_block + trailer block(1B)
    /// GIF89a, 2x2, global_color_table_flag=1 (4色), image_block with LZW sub-blocks
    /// </summary>
    public static byte[] CreateGifWithImageBlock()
    {
        using var ms = new MemoryStream();

        // signature + version
        ms.Write(Encoding.ASCII.GetBytes("GIF"));
        ms.Write(Encoding.ASCII.GetBytes("89a"));

        // === logical_screen_desc ===
        var buf = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, 2); // width
        ms.Write(buf);
        BinaryPrimitives.WriteUInt16LittleEndian(buf, 2); // height
        ms.Write(buf);

        // packed: global_color_table_flag=1, color_resolution=0, sort_flag=0, size_of_global_color_table=1 (4 colors)
        // bit7=1, bit6:4=000, bit3=0, bit2:0=001 → 0b10000001 = 0x81
        ms.WriteByte(0x81);
        ms.WriteByte(0x00); // bg_color_index
        ms.WriteByte(0x00); // pixel_aspect_ratio

        // === global color table (4 colors × 3 bytes = 12 bytes) ===
        for (var i = 0; i < 4; i++)
        {
            ms.WriteByte((byte)(i * 80)); // R
            ms.WriteByte((byte)(i * 80)); // G
            ms.WriteByte((byte)(i * 80)); // B
        }

        // === image block (introducer=0x2C) ===
        ms.WriteByte(0x2C);

        BinaryPrimitives.WriteUInt16LittleEndian(buf, 0); // left
        ms.Write(buf);
        BinaryPrimitives.WriteUInt16LittleEndian(buf, 0); // top
        ms.Write(buf);
        BinaryPrimitives.WriteUInt16LittleEndian(buf, 2); // width
        ms.Write(buf);
        BinaryPrimitives.WriteUInt16LittleEndian(buf, 2); // height
        ms.Write(buf);
        ms.WriteByte(0x00); // packed: no local color table

        ms.WriteByte(0x02); // lzw_min_code_size

        // LZW sub-blocks: [size=5][data...][size=3][data...][0x00]
        ms.WriteByte(0x05); // sub-block 1 size
        ms.Write(new byte[] { 0x04, 0x01, 0x00, 0x00, 0x02 }); // sub-block 1 data
        ms.WriteByte(0x03); // sub-block 2 size
        ms.Write(new byte[] { 0x05, 0x00, 0x00 }); // sub-block 2 data
        ms.WriteByte(0x00); // sub-block terminator

        // === trailer ===
        ms.WriteByte(0x3B);

        return ms.ToArray();
    }
}
