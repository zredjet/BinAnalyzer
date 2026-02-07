using System.Buffers.Binary;

namespace BinAnalyzer.Integration.Tests;

public static class SevenZipTestDataGenerator
{
    /// <summary>
    /// 最小7zファイル: signature_header(32B) + next_header(2B) = 34バイト
    /// 6Bシグネチャ + version(2B) + start_header_crc(4B) + next_header_offset(8B) + next_header_size(8B) + next_header_crc(4B) + header_data(2B)
    /// </summary>
    public static byte[] CreateMinimal7z()
    {
        var data = new byte[34];
        var span = data.AsSpan();
        var pos = 0;

        // === signature_header (32 bytes) ===
        // signature: 37 7A BC AF 27 1C
        data[0] = 0x37;
        data[1] = 0x7A;
        data[2] = 0xBC;
        data[3] = 0xAF;
        data[4] = 0x27;
        data[5] = 0x1C;
        pos = 6;

        // major_version: 0
        data[pos] = 0; pos += 1;

        // minor_version: 4
        data[pos] = 4; pos += 1;

        // start_header_crc: 0 (dummy)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;

        // next_header_offset: 0 (relative to end of signature header = byte 32)
        BinaryPrimitives.WriteUInt64LittleEndian(span[pos..], 0); pos += 8;

        // next_header_size: 2
        BinaryPrimitives.WriteUInt64LittleEndian(span[pos..], 2); pos += 8;

        // next_header_crc: 0 (dummy)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;

        // === next_header (at offset 32, 2 bytes) ===
        // header_data: 2 dummy bytes
        data[pos] = 0x01; pos += 1; // Header property ID
        data[pos] = 0x00;           // end marker

        return data;
    }
}
