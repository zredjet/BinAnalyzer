using System.Buffers.Binary;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class WebpTestDataGenerator
{
    /// <summary>
    /// 最小WebPファイル: RIFF(4B) + file_size(4B) + WEBP(4B) + VP8 chunk(chunk_id(4B) + chunk_size(4B) + data(10B)) = 30バイト
    /// SimpleフォーマットのVP8キーフレーム（1x1ピクセル）
    /// </summary>
    public static byte[] CreateMinimalWebp()
    {
        var data = new byte[30];
        var span = data.AsSpan();
        var pos = 0;

        // riff_magic: "RIFF"
        data[0] = 0x52; data[1] = 0x49; data[2] = 0x46; data[3] = 0x46;
        pos = 4;

        // file_size: 22 (total - 8)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 22); pos += 4;

        // webp_magic: "WEBP"
        data[pos] = 0x57; data[pos + 1] = 0x45; data[pos + 2] = 0x42; data[pos + 3] = 0x50;
        pos += 4;

        // === VP8 chunk ===
        // chunk_id: "VP8 "
        Encoding.ASCII.GetBytes("VP8 ").CopyTo(span[pos..]); pos += 4;

        // chunk_size: 10
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 10); pos += 4;

        // VP8 data (vp8_data struct):
        // frame_tag: 3 bytes bitfield
        // frame_type=0(keyframe), version=0, show_frame=1, partition_size=4
        // bit0=0(key), bits3:1=000(ver), bit4=1(show), bits23:5=4(part_size)
        // = 0b 00000000_00000000_00010000 = byte0=0x10, byte1=0x00, byte2=0x00
        data[pos] = 0x10; pos += 1;
        data[pos] = 0x00; pos += 1;
        data[pos] = 0x00; pos += 1;

        // start_code: 0x9D 0x01 0x2A
        data[pos] = 0x9D; pos += 1;
        data[pos] = 0x01; pos += 1;
        data[pos] = 0x2A; pos += 1;

        // width_and_scale: 1 (16-bit LE)
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 1); pos += 2;

        // height_and_scale: 1 (16-bit LE)
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 1);

        return data;
    }
}
