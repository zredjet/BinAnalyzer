using System.Buffers.Binary;

namespace BinAnalyzer.Integration.Tests;

public static class FlacTestDataGenerator
{
    /// <summary>
    /// 最小FLACファイル: magic(4B) + STREAMINFO metadata_block(4B header + 34B data) = 42バイト
    /// header_byte bit7=1(is_last), block_type=0(STREAMINFO)
    /// 44100Hz, 2ch, 16bit, 0 samples
    /// </summary>
    public static byte[] CreateMinimalFlac()
    {
        var data = new byte[42];
        var span = data.AsSpan();
        var pos = 0;

        // magic: "fLaC"
        data[0] = 0x66; // 'f'
        data[1] = 0x4C; // 'L'
        data[2] = 0x61; // 'a'
        data[3] = 0x43; // 'C'
        pos = 4;

        // === metadata_block header (4 bytes) ===
        // header_byte: is_last=1 (bit7), block_type=0 (STREAMINFO, bits 6:0)
        // = 0b10000000 = 0x80
        data[pos] = 0x80; pos += 1;

        // length: 34 (24-bit big-endian)
        data[pos] = 0x00; pos += 1; // length_b0
        data[pos] = 0x00; pos += 1; // length_b1
        data[pos] = 0x22; pos += 1; // length_b2 (34)

        // === STREAMINFO (34 bytes) ===
        // min_block_size: 4096
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 4096); pos += 2;

        // max_block_size: 4096
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 4096); pos += 2;

        // min_frame_size: 0 (3 bytes)
        data[pos] = 0; pos += 1; // min_frame_size_b0
        data[pos] = 0; pos += 1; // min_frame_size_b1
        data[pos] = 0; pos += 1; // min_frame_size_b2

        // max_frame_size: 0 (3 bytes)
        data[pos] = 0; pos += 1; // max_frame_size_b0
        data[pos] = 0; pos += 1; // max_frame_size_b1
        data[pos] = 0; pos += 1; // max_frame_size_b2

        // sample_rate_channels_bps_samples: 8 bytes bitfield (64 bits)
        // sample_rate(20 bits) = 44100 = 0xAC44
        // channels(3 bits) = 1 (stereo = 2 channels, stored as channels-1)
        // bps(5 bits) = 15 (16 bits, stored as bps-1)
        // total_samples(36 bits) = 0
        //
        // Bit layout (63 downto 0):
        // [63:44] sample_rate = 44100 = 0xAC44 (20 bits)
        // [43:41] channels = 1 (3 bits)
        // [40:36] bps = 15 (5 bits)
        // [35:0] total_samples = 0 (36 bits)
        //
        // Byte 0: bits 63:56 = sample_rate[19:12] = 0xAC
        data[pos] = 0xAC; pos += 1;
        // Byte 1: bits 55:48 = sample_rate[11:4] = 0x44
        data[pos] = 0x44; pos += 1;
        // Byte 2: bits 47:40 = sample_rate[3:0] | channels[2:0] | bps[4]
        //       = 0000 | 001 | 1 = 0x03
        data[pos] = 0x03; pos += 1;
        // Byte 3: bits 39:32 = bps[3:0] | total_samples[35:32]
        //       = 1110 | 0000 = 0xF0
        data[pos] = 0xF0; pos += 1;
        // Bytes 4-7: total_samples[31:0] = 0
        data[pos] = 0; pos += 1;
        data[pos] = 0; pos += 1;
        data[pos] = 0; pos += 1;
        data[pos] = 0; pos += 1;

        // md5: 16 bytes of zeros
        // pos += 16; (already zeroed)

        return data;
    }
}
