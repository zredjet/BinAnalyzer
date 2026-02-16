using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class OggTestDataGenerator
{
    /// <summary>
    /// 最小2ページ OGG Vorbis ファイル（87バイト）:
    /// Page 1 (BOS): OggS + header(23B) + segment_table(1B) + Vorbis ID(30B) = 58B
    /// Page 2 (EOS): OggS + header(23B) + segment_table(1B) + data(1B) = 29B
    /// </summary>
    public static byte[] CreateMinimalOgg()
    {
        var ms = new MemoryStream();

        // === Page 1: BOS (Beginning of Stream) with Vorbis Identification Header ===
        WritePage(ms,
            headerType: 0x02,     // BOS flag
            granulePosition: 0,
            serialNumber: 1,
            pageSequenceNumber: 0,
            segmentData: CreateVorbisIdentificationPacket());

        // === Page 2: EOS (End of Stream) with minimal data ===
        WritePage(ms,
            headerType: 0x04,     // EOS flag
            granulePosition: 0,
            serialNumber: 1,
            pageSequenceNumber: 1,
            segmentData: new byte[] { 0x00 });

        return ms.ToArray();
    }

    private static byte[] CreateVorbisIdentificationPacket()
    {
        var ms = new MemoryStream();

        // Packet type: 1 (identification)
        ms.WriteByte(0x01);

        // Codec ID: "vorbis" (6 bytes)
        ms.Write(Encoding.ASCII.GetBytes("vorbis"));

        // Vorbis version: 0
        WriteUInt32LE(ms, 0);

        // Channels: 1 (mono)
        ms.WriteByte(0x01);

        // Sample rate: 44100
        WriteUInt32LE(ms, 44100);

        // Bitrate maximum: 0
        WriteUInt32LE(ms, 0);

        // Bitrate nominal: 128000
        WriteUInt32LE(ms, 128000);

        // Bitrate minimum: 0
        WriteUInt32LE(ms, 0);

        // Blocksize info: 0xB8 (blocksize_0=8, blocksize_1=11)
        ms.WriteByte(0xB8);

        // Framing flag: 1
        ms.WriteByte(0x01);

        return ms.ToArray(); // 30 bytes
    }

    private static void WritePage(MemoryStream ms,
        byte headerType, ulong granulePosition,
        uint serialNumber, uint pageSequenceNumber,
        byte[] segmentData)
    {
        // Capture pattern: "OggS"
        ms.Write(Encoding.ASCII.GetBytes("OggS"));

        // Version: 0
        ms.WriteByte(0x00);

        // Header type
        ms.WriteByte(headerType);

        // Granule position (8 bytes LE)
        WriteUInt64LE(ms, granulePosition);

        // Serial number (4 bytes LE)
        WriteUInt32LE(ms, serialNumber);

        // Page sequence number (4 bytes LE)
        WriteUInt32LE(ms, pageSequenceNumber);

        // CRC32: 0 (not computed for test data)
        WriteUInt32LE(ms, 0);

        // Number of segments: 1
        ms.WriteByte(0x01);

        // Segment table: single entry with segment data length
        ms.WriteByte((byte)segmentData.Length);

        // Segment data
        ms.Write(segmentData);
    }

    private static void WriteUInt32LE(MemoryStream ms, uint value)
    {
        ms.WriteByte((byte)(value & 0xFF));
        ms.WriteByte((byte)((value >> 8) & 0xFF));
        ms.WriteByte((byte)((value >> 16) & 0xFF));
        ms.WriteByte((byte)((value >> 24) & 0xFF));
    }

    private static void WriteUInt64LE(MemoryStream ms, ulong value)
    {
        ms.WriteByte((byte)(value & 0xFF));
        ms.WriteByte((byte)((value >> 8) & 0xFF));
        ms.WriteByte((byte)((value >> 16) & 0xFF));
        ms.WriteByte((byte)((value >> 24) & 0xFF));
        ms.WriteByte((byte)((value >> 32) & 0xFF));
        ms.WriteByte((byte)((value >> 40) & 0xFF));
        ms.WriteByte((byte)((value >> 48) & 0xFF));
        ms.WriteByte((byte)((value >> 56) & 0xFF));
    }
}
