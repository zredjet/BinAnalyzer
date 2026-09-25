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

    /// <summary>
    /// Ogg Opus（REQ-188）。1 ページ目（BOS）に OpusHead、2 ページ目に OpusTags（ARTIST=Tester）、3 ページ目（EOS）に
    /// 途中に 'OggS' を含む 20 バイトの音声データ。CRC は Ogg の CRC-32 で正しく計算する。
    /// </summary>
    public static byte[] CreateOpusOgg()
    {
        var head = "OpusHead"u8.ToArray().Concat(new byte[] { 1, 2, 0x38, 0x01, 0x80, 0xBB, 0, 0, 0, 0, 0 }).ToArray();
        var tags = "OpusTags"u8.ToArray().Concat(BitConverter.GetBytes(3)).Concat("gen"u8.ToArray()).Concat(BitConverter.GetBytes(1))
            .Concat(BitConverter.GetBytes(13)).Concat("ARTIST=Tester"u8.ToArray()).ToArray();
        var audio = new byte[] { 0xFC, 1, 2, 3, 4, 5, 6, 7, (byte)'O', (byte)'g', (byte)'g', (byte)'S', 8, 9, 10, 11, 12, 13, 14, 15 };
        return Page(2, 0, 0, head).Concat(Page(0, 0, 1, tags)).Concat(Page(4, 960, 2, audio)).ToArray();
    }

    private static byte[] Page(byte headerType, long granule, uint sequence, byte[] packet)
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write("OggS"u8);
        w.Write((byte)0);
        w.Write(headerType);
        w.Write(granule);
        w.Write(0x1234u);
        w.Write(sequence);
        w.Write(0u);
        var lacing = new List<byte>();
        var remaining = packet.Length;
        while (remaining >= 255) { lacing.Add(255); remaining -= 255; }
        lacing.Add((byte)remaining);
        w.Write((byte)lacing.Count);
        w.Write(lacing.ToArray());
        w.Write(packet);
        var page = ms.ToArray();
        BitConverter.GetBytes(OggCrc(page)).CopyTo(page, 22);
        return page;
    }

    /// <summary>Ogg の CRC-32（多項式 0x04C11DB7、初期値 0、非反転）。</summary>
    private static uint OggCrc(byte[] data)
    {
        uint crc = 0;
        foreach (var b in data)
        {
            crc ^= (uint)b << 24;
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x80000000) != 0 ? (crc << 1) ^ 0x04C11DB7 : crc << 1;
        }
        return crc;
    }
}
