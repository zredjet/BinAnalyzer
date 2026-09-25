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

    /// <summary>
    /// ID3v2.4 + MPEG-1 Layer III（128 kbps・44.1 kHz・モノラル）+ ID3v1.1 の MP3（REQ-188）。ID3v2.4 には TIT2（UTF-8 "タイトル"）・
    /// TPE1（BOM 付き UTF-16 "Artist"）・200 バイトの TXXX（シンクセーフ整数の大きさ）と 16 バイトの詰め物、最初のフレームに Info ヘッダ、
    /// 続いて 417 バイトのフレーム 2 個、末尾に ID3v1.1（"V1Title"、トラック 7、ジャンル 17）。
    /// </summary>
    public static byte[] CreateMp3WithTags()
    {
        static byte[] Syncsafe(int v) => [(byte)((v >> 21) & 0x7F), (byte)((v >> 14) & 0x7F), (byte)((v >> 7) & 0x7F), (byte)(v & 0x7F)];
        static byte[] Frame(string id, byte[] body) => Encoding.ASCII.GetBytes(id).Concat(Syncsafe(body.Length)).Concat(new byte[2]).Concat(body).ToArray();
        var frames = Frame("TIT2", new byte[] { 3 }.Concat(Encoding.UTF8.GetBytes("タイトル")).ToArray())
            .Concat(Frame("TPE1", new byte[] { 1, 0xFF, 0xFE }.Concat(Encoding.Unicode.GetBytes("Artist")).ToArray()))
            .Concat(Frame("TXXX", new byte[] { 0 }.Concat(Encoding.ASCII.GetBytes("key\0" + new string('x', 195))).ToArray()))
            .Concat(new byte[16]).ToArray();
        var ms = new MemoryStream();
        ms.Write("ID3"u8);
        ms.Write([4, 0, 0]);
        ms.Write(Syncsafe(frames.Length));
        ms.Write(frames);

        byte[] MpegFrame(bool info)
        {
            var f = new byte[417];
            new byte[] { 0xFF, 0xFB, 0x90, 0xC4 }.CopyTo(f, 0);
            if (info)
            {
                "Info"u8.CopyTo(f.AsSpan(4 + 17));
                BinaryPrimitives.WriteUInt32BigEndian(f.AsSpan(25), 0x3);     // frames | bytes
                BinaryPrimitives.WriteUInt32BigEndian(f.AsSpan(29), 2);
                BinaryPrimitives.WriteUInt32BigEndian(f.AsSpan(33), 417 * 3);
                "LAME3.100"u8.CopyTo(f.AsSpan(37));
            }
            return f;
        }
        ms.Write(MpegFrame(true));
        ms.Write(MpegFrame(false));
        ms.Write(MpegFrame(false));

        var v1 = new byte[128];
        "TAG"u8.CopyTo(v1);
        "V1Title"u8.CopyTo(v1.AsSpan(3));
        "1999"u8.CopyTo(v1.AsSpan(93));
        v1[126] = 7;
        v1[127] = 17;
        ms.Write(v1);
        return ms.ToArray();
    }

    /// <summary>ID3 タグの無い MPEG-2 Layer III（64 kbps・22.05 kHz・モノラル、208 バイト）のフレーム 2 個（REQ-188）。</summary>
    public static byte[] CreateMp3WithoutTags()
    {
        var data = new byte[208 * 2];
        for (var i = 0; i < 2; i++)
            new byte[] { 0xFF, 0xF3, 0x80, 0xC4 }.CopyTo(data, i * 208);
        return data;
    }
}
