using System.Buffers.Binary;

namespace BinAnalyzer.Integration.Tests;

public static class WavTestDataGenerator
{
    /// <summary>
    /// PCM, 1ch, 8000Hz, 8bit, 4サンプルの最小WAVを生成する。
    /// RIFFヘッダー(12) + fmtチャンク(24) + dataチャンク(12) = 48バイト
    /// </summary>
    public static byte[] CreateMinimalWav()
    {
        var data = new byte[48];
        var span = data.AsSpan();

        // RIFFヘッダー (12 bytes)
        data[0] = 0x52; // 'R'
        data[1] = 0x49; // 'I'
        data[2] = 0x46; // 'F'
        data[3] = 0x46; // 'F'
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], 40);  // file_size (total - 8)
        data[8] = 0x57;  // 'W'
        data[9] = 0x41;  // 'A'
        data[10] = 0x56; // 'V'
        data[11] = 0x45; // 'E'

        // fmt チャンク (24 bytes: header 8 + data 16)
        data[12] = 0x66; // 'f'
        data[13] = 0x6D; // 'm'
        data[14] = 0x74; // 't'
        data[15] = 0x20; // ' '
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], 16);    // chunk_size
        BinaryPrimitives.WriteUInt16LittleEndian(span[20..], 1);     // audio_format = PCM
        BinaryPrimitives.WriteUInt16LittleEndian(span[22..], 1);     // num_channels
        BinaryPrimitives.WriteUInt32LittleEndian(span[24..], 8000);  // sample_rate
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..], 8000);  // byte_rate (8000 * 1 * 1)
        BinaryPrimitives.WriteUInt16LittleEndian(span[32..], 1);     // block_align (1 * 1)
        BinaryPrimitives.WriteUInt16LittleEndian(span[34..], 8);     // bits_per_sample

        // data チャンク (12 bytes: header 8 + data 4)
        data[36] = 0x64; // 'd'
        data[37] = 0x61; // 'a'
        data[38] = 0x74; // 't'
        data[39] = 0x61; // 'a'
        BinaryPrimitives.WriteUInt32LittleEndian(span[40..], 4);     // chunk_size
        data[44] = 0x80; // sample 0 (silence for 8-bit unsigned)
        data[45] = 0x80; // sample 1
        data[46] = 0x80; // sample 2
        data[47] = 0x80; // sample 3

        return data;
    }

    /// <summary>
    /// LIST-INFO付きWAV: RIFF(12) + fmt(24) + LIST(8 + 4 + INAM(14) + ISFT(12)) = 74バイト
    /// </summary>
    public static byte[] CreateWavWithListInfo()
    {
        var data = new byte[74];
        var span = data.AsSpan();

        // RIFFヘッダー (12 bytes)
        data[0] = 0x52; // 'R'
        data[1] = 0x49; // 'I'
        data[2] = 0x46; // 'F'
        data[3] = 0x46; // 'F'
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], 66);  // file_size (74 - 8)
        data[8] = 0x57;  // 'W'
        data[9] = 0x41;  // 'A'
        data[10] = 0x56; // 'V'
        data[11] = 0x45; // 'E'

        // fmt チャンク (24 bytes: header 8 + data 16)
        data[12] = 0x66; // 'f'
        data[13] = 0x6D; // 'm'
        data[14] = 0x74; // 't'
        data[15] = 0x20; // ' '
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], 16);    // chunk_size
        BinaryPrimitives.WriteUInt16LittleEndian(span[20..], 1);     // audio_format = PCM
        BinaryPrimitives.WriteUInt16LittleEndian(span[22..], 1);     // num_channels
        BinaryPrimitives.WriteUInt32LittleEndian(span[24..], 8000);  // sample_rate
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..], 8000);  // byte_rate
        BinaryPrimitives.WriteUInt16LittleEndian(span[32..], 1);     // block_align
        BinaryPrimitives.WriteUInt16LittleEndian(span[34..], 8);     // bits_per_sample

        // LIST チャンク (header 8 + list_type 4 + sub_chunks 26 = 38 bytes)
        var pos = 36;
        data[pos] = 0x4C; // 'L'
        data[pos + 1] = 0x49; // 'I'
        data[pos + 2] = 0x53; // 'S'
        data[pos + 3] = 0x54; // 'T'
        BinaryPrimitives.WriteUInt32LittleEndian(span[(pos + 4)..], 30); // chunk_size (4 + 14 + 12)
        pos += 8;

        // list_type: "INFO"
        data[pos] = 0x49; // 'I'
        data[pos + 1] = 0x4E; // 'N'
        data[pos + 2] = 0x46; // 'F'
        data[pos + 3] = 0x4F; // 'O'
        pos += 4;

        // INFO sub-chunk 1: INAM (chunk_id=4 + chunk_size=4 + value=5 + padding=1 = 14 bytes)
        data[pos] = 0x49; // 'I'
        data[pos + 1] = 0x4E; // 'N'
        data[pos + 2] = 0x41; // 'A'
        data[pos + 3] = 0x4D; // 'M'
        BinaryPrimitives.WriteUInt32LittleEndian(span[(pos + 4)..], 5); // chunk_size=5
        // value: "Test\0" (5 bytes)
        data[pos + 8] = 0x54; // 'T'
        data[pos + 9] = 0x65; // 'e'
        data[pos + 10] = 0x73; // 's'
        data[pos + 11] = 0x74; // 't'
        data[pos + 12] = 0x00; // '\0'
        // padding: 1 byte (chunk_size 5 is odd)
        data[pos + 13] = 0x00;
        pos += 14;

        // INFO sub-chunk 2: ISFT (chunk_id=4 + chunk_size=4 + value=4 = 12 bytes, no padding)
        data[pos] = 0x49; // 'I'
        data[pos + 1] = 0x53; // 'S'
        data[pos + 2] = 0x46; // 'F'
        data[pos + 3] = 0x54; // 'T'
        BinaryPrimitives.WriteUInt32LittleEndian(span[(pos + 4)..], 4); // chunk_size=4
        // value: "App\0" (4 bytes)
        data[pos + 8] = 0x41; // 'A'
        data[pos + 9] = 0x70; // 'p'
        data[pos + 10] = 0x70; // 'p'
        data[pos + 11] = 0x00; // '\0'

        return data;
    }
}
