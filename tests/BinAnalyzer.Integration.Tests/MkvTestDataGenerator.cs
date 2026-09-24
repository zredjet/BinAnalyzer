using System.Buffers.Binary;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

/// <summary>
/// 最小の WebM（Matroska）を組み立てる（REQ-147）。EBML ヘッダ + サイズ不定の Segment（ライブ配信の形、8 バイトの VINT）に
/// Info・Tracks（映像 1 トラック）・Cluster（SimpleBlock 1 つ）を入れる。
/// </summary>
public static class MkvTestDataGenerator
{
    public static byte[] CreateMinimalWebm()
    {
        var ebml = Element(0x1A45DFA3,
            Element(0x4286, 1), Element(0x42F7, 1), Element(0x42F2, 4), Element(0x42F3, 8),
            Element(0x4282, Encoding.ASCII.GetBytes("webm")), Element(0x4287, 4), Element(0x4285, 2));

        var info = Element(0x1549A966,
            Element(0x2AD7B1, [0x0F, 0x42, 0x40]),                     // TimestampScale = 1000000
            Element(0x4489, Float64(1000.0)),                           // Duration
            Element(0x4D80, Encoding.UTF8.GetBytes("BinAnalyzer")),     // MuxingApp
            Element(0x7BA9, Encoding.UTF8.GetBytes("テスト")));           // Title

        var tracks = Element(0x1654AE6B,
            Element(0xAE,
                Element(0xD7, 1),                                       // TrackNumber
                Element(0x73C5, [0x12, 0x34, 0x56, 0x78, 0x9A]),        // TrackUID（5 バイト）
                Element(0x83, 1),                                       // TrackType = video
                Element(0x86, Encoding.ASCII.GetBytes("V_VP8")),        // CodecID
                Element(0xE0, Element(0xB0, 16), Element(0xBA, 16))));  // Video: 16x16

        byte[] simpleBlock = [0x81, 0x00, 0x00, 0x80, .. "frame"u8];  // track 1, timecode 0, keyframe
        var cluster = Element(0x1F43B675, Element(0xE7, 0), Element(0xA3, simpleBlock));

        // Segment はサイズ不定（8 バイトの VINT で値ビットがすべて 1）。ファイルの残りすべてが子要素
        byte[] unknownSize = [0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
        return Concat(ebml, Id(0x18538067), unknownSize, info, tracks, cluster);
    }

    private static byte[] Element(uint id, params byte[][] children) => Element(id, Concat(children));

    private static byte[] Element(uint id, byte[] payload) => Concat(Id(id), Size(payload.Length), payload);

    private static byte[] Element(uint id, int unsignedValue) => Element(id, [(byte)unsignedValue]);

    private static byte[] Id(uint id)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, id);
        return bytes.SkipWhile(b => b == 0).ToArray();
    }

    /// <summary>サイズの VINT（127 未満は 1 バイト、それ以上は 2 バイト）。</summary>
    private static byte[] Size(int size) => size < 0x7F
        ? [(byte)(0x80 | size)]
        : [(byte)(0x40 | (size >> 8)), (byte)size];

    private static byte[] Float64(double v)
    {
        var b = new byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(b, v);
        return b;
    }

    private static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
}
