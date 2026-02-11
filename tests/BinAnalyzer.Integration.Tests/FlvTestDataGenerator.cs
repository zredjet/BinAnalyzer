using System.Buffers.Binary;

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
}
