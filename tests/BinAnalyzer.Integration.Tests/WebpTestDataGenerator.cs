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

    /// <summary>
    /// 拡張形式のアニメーション WebP（REQ-188）。VP8X（アルファ + アニメーション + XMP）、ANIM、
    /// ANMF 2 つ（それぞれ VP8L のサブチャンク）、XMP。VP8L は 5 バイトのヘッダ（2x3、アルファあり）+ ダミーのビットストリーム。
    /// </summary>
    public static byte[] CreateAnimatedWebp()
    {
        var body = new List<byte>();
        body.AddRange("WEBP"u8.ToArray());
        body.AddRange(Chunk("VP8X", [0x16, 0, 0, 0, .. U24(4 - 1), .. U24(4 - 1)]));   // alpha(0x10) | xmp(0x04) | animation(0x02)
        body.AddRange(Chunk("ANIM", [0x00, 0x00, 0xFF, 0xFF, 3, 0]));                  // 背景 BGRA、3 回繰り返し
        body.AddRange(Chunk("ANMF", [.. U24(0), .. U24(1), .. U24(2 - 1), .. U24(3 - 1), .. U24(120), 0x02, .. Chunk("VP8L", Vp8l(2, 3))]));
        body.AddRange(Chunk("ANMF", [.. U24(1), .. U24(0), .. U24(2 - 1), .. U24(3 - 1), .. U24(80), 0x01, .. Chunk("VP8L", Vp8l(2, 3))]));
        body.AddRange(Chunk("XMP ", "<x:xmpmeta/>"u8.ToArray()));
        return [.. "RIFF"u8, .. U32(body.Count), .. body];
    }

    /// <summary>VP8L: シグネチャ + 32 ビット LE（幅 - 1 : 14、高さ - 1 : 14、アルファ : 1、バージョン : 3）+ ダミー 1 バイト。</summary>
    private static byte[] Vp8l(int width, int height)
    {
        var header = (uint)(width - 1) | ((uint)(height - 1) << 14) | (1u << 28);
        return [0x2F, .. U32((int)header), 0x00];
    }

    private static byte[] Chunk(string id, byte[] data) =>
        [.. System.Text.Encoding.ASCII.GetBytes(id), .. U32(data.Length), .. data, .. data.Length % 2 == 1 ? new byte[] { 0 } : []];

    private static byte[] U24(int v) => [(byte)v, (byte)(v >> 8), (byte)(v >> 16)];

    private static byte[] U32(int v) => [(byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24)];
}
