using System.Buffers.Binary;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class Mp3TestDataGenerator
{
    /// <summary>
    /// 最小MP3ファイル: ID3v2ヘッダ(10B) + 1 ID3 frame(10B+2B) + MPEGフレームヘッダ(2B) + audio_properties(1B) + channel_mode(1B) = 26バイト
    /// ID3v2.3, フレーム"TIT2"(2バイトデータ), MPEG1 Layer III 128kbps 44100Hz Stereo
    /// </summary>
    public static byte[] CreateMinimalMp3()
    {
        var data = new byte[26];
        var span = data.AsSpan();
        var pos = 0;

        // === id3v2_header ===
        // magic: "ID3"
        Encoding.ASCII.GetBytes("ID3").CopyTo(span[pos..]); pos += 3;

        // version: 3 (ID3v2.3)
        data[pos] = 3; pos += 1;

        // revision: 0
        data[pos] = 0; pos += 1;

        // flags: 0 (1 byte bitfield)
        data[pos] = 0; pos += 1;

        // size: syncsafe integer for 12 bytes (1 frame: 4+4+2+2=12)
        // 12 = 0b0001100 => syncsafe: 0,0,0,12
        data[pos] = 0; pos += 1; // size_b0
        data[pos] = 0; pos += 1; // size_b1
        data[pos] = 0; pos += 1; // size_b2
        data[pos] = 12; pos += 1; // size_b3
        // tag_size = (0<<21)|(0<<14)|(0<<7)|12 = 12

        // === id3v2_body (12 bytes, switch boundary) ===
        // frame: TIT2
        // frame_id: "TIT2" (4 bytes)
        Encoding.ASCII.GetBytes("TIT2").CopyTo(span[pos..]); pos += 4;

        // frame_size: 2
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 2); pos += 4;

        // frame_flags: 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // frame_data: 2 bytes
        data[pos] = 0x03; pos += 1; // UTF-8 encoding byte
        data[pos] = 0x41; pos += 1; // 'A'

        // === mpeg_frame_header (2 bytes) ===
        // MPEG1, Layer III, no CRC
        // sync(11 bits)=0x7FF, version(2)=11(MPEG1), layer(2)=01(Layer III), protection(1)=1(no CRC)
        // = 1111 1111 1111 1011 = 0xFFFB
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0xFFFB); pos += 2;

        // === audio_properties (1 byte) ===
        // bitrate_index=9(128kbps), sample_rate_index=0(44100Hz), padding=0, private=0
        // = 1001 00 0 0 = 0x90
        data[pos] = 0x90; pos += 1;

        // === channel_mode_and_extension (1 byte) ===
        // channel_mode=0(stereo), mode_ext=0, copyright=0, original=1, emphasis=0
        // = 00 00 0 1 00 = 0x04
        data[pos] = 0x04;

        return data;
    }
}
