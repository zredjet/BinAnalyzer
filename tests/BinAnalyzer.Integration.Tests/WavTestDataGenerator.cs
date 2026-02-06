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
}
