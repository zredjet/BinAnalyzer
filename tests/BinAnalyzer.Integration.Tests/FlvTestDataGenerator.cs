using System.Buffers.Binary;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class FlvTestDataGenerator
{
    /// <summary>
    /// 最小FLVバイナリ: signature(3B) + version(1B) + flags(1B) + data_offset(4B) + prev_tag_size_0(4B) + 1 tag(16B) = 29バイト
    /// audioタグ1つ（データサイズ=1バイト）を含む
    /// </summary>
    public static byte[] CreateMinimalFlv()
    {
        var data = new byte[29];
        var span = data.AsSpan();
        var pos = 0;

        // signature: "FLV"
        data[0] = 0x46; // 'F'
        data[1] = 0x4C; // 'L'
        data[2] = 0x56; // 'V'
        pos = 3;

        // version: 1
        data[pos] = 0x01; pos += 1;

        // flags: 1 byte bitfield (has_audio=1, has_video=0)
        // bit 0 = has_video = 0, bit 2 = has_audio = 1 => 0x04
        data[pos] = 0x04; pos += 1;

        // data_offset: 9 (standard header size)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 9); pos += 4;

        // prev_tag_size_0: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // === FLV tag (audio, 1 byte data) ===
        // tag_type: 8 (audio)
        data[pos] = 8; pos += 1;

        // data_size: 24-bit = 1 (3 bytes big-endian)
        data[pos] = 0x00; pos += 1; // data_size_b0 (MSB)
        data[pos] = 0x00; pos += 1; // data_size_b1
        data[pos] = 0x01; pos += 1; // data_size_b2 (LSB)

        // timestamp: 24-bit + ext = 0
        data[pos] = 0x00; pos += 1; // timestamp_b0
        data[pos] = 0x00; pos += 1; // timestamp_b1
        data[pos] = 0x00; pos += 1; // timestamp_b2
        data[pos] = 0x00; pos += 1; // timestamp_ext

        // stream_id: 3 bytes = 0
        data[pos] = 0x00; pos += 1;
        data[pos] = 0x00; pos += 1;
        data[pos] = 0x00; pos += 1;

        // data: 1 byte
        data[pos] = 0xAA; pos += 1;

        // prev_tag_size: 12 (11 header + 1 data)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 12);

        return data;
    }

    /// <summary>
    /// FLV with video tag: signature(3B) + version(1B) + flags(1B) + data_offset(4B) + prev_tag_size_0(4B) + 1 video tag(16B) = 29バイト
    /// video tag: tag_type=9, data_size=1, data=0x17 (keyframe, AVC/H.264)
    /// </summary>
    public static byte[] CreateFlvWithVideoTag()
    {
        var data = new byte[29];
        var span = data.AsSpan();
        var pos = 0;

        // signature: "FLV"
        data[0] = 0x46; data[1] = 0x4C; data[2] = 0x56;
        pos = 3;

        // version: 1
        data[pos] = 0x01; pos += 1;

        // flags: has_video=1 (bit 0)
        data[pos] = 0x01; pos += 1;

        // data_offset: 9
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 9); pos += 4;

        // prev_tag_size_0: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // === FLV tag (video, 1 byte data) ===
        data[pos] = 9; pos += 1; // tag_type: video

        // data_size: 1 (24-bit)
        data[pos] = 0x00; pos += 1;
        data[pos] = 0x00; pos += 1;
        data[pos] = 0x01; pos += 1;

        // timestamp: 0
        data[pos] = 0x00; pos += 1;
        data[pos] = 0x00; pos += 1;
        data[pos] = 0x00; pos += 1;
        data[pos] = 0x00; pos += 1;

        // stream_id: 0
        data[pos] = 0x00; pos += 1;
        data[pos] = 0x00; pos += 1;
        data[pos] = 0x00; pos += 1;

        // data: 0x17 = keyframe(1) + AVC(7)
        data[pos] = 0x17; pos += 1;

        // prev_tag_size: 12
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 12);

        return data;
    }

    /// <summary>
    /// onMetaData（AMF0 の連想配列: duration = 1.5、width = 64、encoder = "gen"、stereo = true）、AVC の sequence header と
    /// 表示時刻のずれ −40 ms の NALU、AAC の sequence header を持つ FLV（REQ-188）。PreviousTagSize はすべて正しい値にする。
    /// </summary>
    public static byte[] CreateFlvWithMetadata()
    {
        static byte[] Be16(int v) => [(byte)(v >> 8), (byte)v];
        static byte[] Be24(int v) => [(byte)(v >> 16), (byte)(v >> 8), (byte)v];
        static byte[] Be32(int v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];
        static byte[] Str(string s) => Be16(s.Length).Concat(Encoding.ASCII.GetBytes(s)).ToArray();
        static byte[] Num(double d) { var b = BitConverter.GetBytes(d); Array.Reverse(b); return new byte[] { 0 }.Concat(b).ToArray(); }

        var meta = new byte[] { 2 }.Concat(Str("onMetaData")).Concat(new byte[] { 8 }).Concat(Be32(4))
            .Concat(Str("duration")).Concat(Num(1.5))
            .Concat(Str("width")).Concat(Num(64))
            .Concat(Str("encoder")).Concat(new byte[] { 2 }).Concat(Str("gen"))
            .Concat(Str("stereo")).Concat(new byte[] { 1, 1 })
            .Concat(new byte[] { 0, 0, 9 }).ToArray();
        var ms = new MemoryStream();
        ms.Write("FLV"u8);
        ms.Write([1, 5]);
        ms.Write(Be32(9));
        ms.Write(Be32(0));
        void Tag(int type, int timestamp, byte[] body)
        {
            ms.WriteByte((byte)type);
            ms.Write(Be24(body.Length));
            ms.Write(Be24(timestamp));
            ms.WriteByte(0);
            ms.Write(Be24(0));
            ms.Write(body);
            ms.Write(Be32(body.Length + 11));
        }
        Tag(18, 0, meta);
        Tag(9, 0, new byte[] { 0x17, 0, 0, 0, 0, 1, 0x64, 0x00, 0x0A, 0xFF });
        Tag(9, 40, new byte[] { 0x27, 1, 0xFF, 0xFF, 0xD8, 0, 0, 0, 1, 0x41 });
        Tag(8, 0, new byte[] { 0xAF, 0, 0x12, 0x10 });
        return ms.ToArray();
    }
}
