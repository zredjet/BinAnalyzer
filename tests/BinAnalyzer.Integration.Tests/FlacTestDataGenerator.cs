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

        // sample_rate_channels_bps_samples: 8 bytes bitfield (64 bits, big-endian)
        // sample_rate(20 bits) = 44100 = 0x0AC44
        // channels(3 bits) = 1 (stereo = 2 channels, stored as channels-1)
        // bps(5 bits) = 15 (16 bits, stored as bps-1)  → 0b01111
        // total_samples(36 bits) = 0
        //
        // Bit layout (63 downto 0):
        // [63:44] sample_rate = 44100 (20 bits)
        // [43:41] channels = 1 (3 bits)
        // [40:36] bps = 15 (5 bits)
        // [35:0] total_samples = 0 (36 bits)
        //
        // Byte 0 (bits 63:56): sample_rate[19:12] = 0x0AC44 >> 12 = 0x0A
        data[pos] = 0x0A; pos += 1;
        // Byte 1 (bits 55:48): sample_rate[11:4] = (0x0AC44 >> 4) & 0xFF = 0xC4
        data[pos] = 0xC4; pos += 1;
        // Byte 2 (bits 47:40): sample_rate[3:0]=0100 | channels=001 | bps[4]=0
        //       = 0100_0010 = 0x42
        data[pos] = 0x42; pos += 1;
        // Byte 3 (bits 39:32): bps[3:0]=1111 | total_samples[35:32]=0000
        //       = 1111_0000 = 0xF0
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

    /// <summary>
    /// FLACファイル with CUESHEET: magic(4B) + STREAMINFO block(4+34=38B, is_last=0) + CUESHEET block
    /// CUESHEET: header(4B) + media_catalog(128B) + lead_in(8B) + flags(1B) + reserved(258B) + num_tracks(1B) + 1 track(36B) = 436B body
    /// track: offset(8) + number(1) + isrc(12) + flags(1) + reserved(13) + num_indices(1) = 36B (but indices follow)
    /// With 1 index: track(36B) + index(12B) = 48B → total cuesheet body = 128+8+1+258+1+48 = 444B
    /// Total: 4 + 38 + 4 + 444 = 490B
    /// </summary>
    public static byte[] CreateFlacWithCuesheet()
    {
        var data = new byte[490];
        var span = data.AsSpan();
        var pos = 0;

        // magic: "fLaC"
        data[0] = 0x66; data[1] = 0x4C; data[2] = 0x61; data[3] = 0x43;
        pos = 4;

        // === STREAMINFO metadata_block (is_last=0) ===
        data[pos] = 0x00; pos += 1; // header_byte: is_last=0, block_type=0
        data[pos] = 0x00; pos += 1; // length_b0
        data[pos] = 0x00; pos += 1; // length_b1
        data[pos] = 0x22; pos += 1; // length_b2 (34)

        // STREAMINFO: 34 bytes (minimal valid)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 4096); pos += 2; // min_block_size
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 4096); pos += 2; // max_block_size
        pos += 3; // min_frame_size (0)
        pos += 3; // max_frame_size (0)
        // bitfield: 44100Hz, stereo, 16-bit, 0 samples
        data[pos] = 0x0A; pos += 1;
        data[pos] = 0xC4; pos += 1;
        data[pos] = 0x42; pos += 1;
        data[pos] = 0xF0; pos += 1;
        pos += 4; // total_samples low bytes
        pos += 16; // md5

        // === CUESHEET metadata_block (is_last=1) ===
        data[pos] = 0x85; pos += 1; // header_byte: is_last=1, block_type=5 (CUESHEET)
        // length: 444 (24-bit big-endian)
        data[pos] = 0x00; pos += 1; // length_b0
        data[pos] = 0x01; pos += 1; // length_b1
        data[pos] = 0xBC; pos += 1; // length_b2 (0x01BC = 444)

        // CUESHEET body:
        // media_catalog: 128 bytes (null-padded)
        data[pos] = (byte)'T'; data[pos + 1] = (byte)'E'; data[pos + 2] = (byte)'S'; data[pos + 3] = (byte)'T';
        pos += 128;

        // lead_in_samples: 88200 (2 seconds at 44100)
        BinaryPrimitives.WriteUInt64BigEndian(span[pos..], 88200); pos += 8;

        // cuesheet_flags: 1 byte bitfield, is_cd=1 (bit 7)
        data[pos] = 0x80; pos += 1;

        // reserved: 258 bytes
        pos += 258;

        // num_tracks: 1
        data[pos] = 1; pos += 1;

        // === cuesheet_track ===
        // track_offset: 0 (8 bytes)
        BinaryPrimitives.WriteUInt64BigEndian(span[pos..], 0); pos += 8;
        // track_number: 1
        data[pos] = 1; pos += 1;
        // isrc: 12 bytes
        pos += 12;
        // track_flags: 1 byte (track_type=0, pre_emphasis=0)
        data[pos] = 0x00; pos += 1;
        // track_reserved: 13 bytes
        pos += 13;
        // num_indices: 1
        data[pos] = 1; pos += 1;

        // === cuesheet_index ===
        // offset: 0 (8 bytes)
        BinaryPrimitives.WriteUInt64BigEndian(span[pos..], 0); pos += 8;
        // index_number: 1
        data[pos] = 1; pos += 1;
        // reserved: 3 bytes
        pos += 3;

        return data;
    }
}
