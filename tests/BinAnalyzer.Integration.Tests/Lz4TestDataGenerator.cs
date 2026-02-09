using System.Buffers.Binary;

namespace BinAnalyzer.Integration.Tests;

public static class Lz4TestDataGenerator
{
    /// <summary>
    /// 最小LZ4フレーム: magic(4B) + FLG(1B) + BD(1B) + header_checksum(1B) + EndMark(4B) = 11バイト
    /// content_size=0, dict_id=0, block_max_size=4(64KB), version=01
    /// </summary>
    public static byte[] CreateMinimalLz4()
    {
        var data = new byte[11];
        var span = data.AsSpan();
        var pos = 0;

        // magic: 0x184D2204 (LE)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x184D2204); pos += 4;

        // FLG byte: version=01(bits 7:6), b_independence=1(bit5), b_checksum=0(bit4),
        //           content_size=0(bit3), content_checksum=0(bit2), reserved=0(bit1), dict_id=0(bit0)
        // = 0b01_1_0_0_0_0_0 = 0x60
        data[pos] = 0x60; pos += 1;

        // BD byte: reserved_bd_high=0(bit7), block_max_size=4(bits 6:4=100), reserved_bd=0(bits 3:0)
        // = 0b0_100_0000 = 0x40
        data[pos] = 0x40; pos += 1;

        // header_checksum: xxHash-32 second byte (dummy valid-ish value)
        data[pos] = 0x82; pos += 1;

        // EndMark (block_size=0, 4 bytes LE)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0);

        return data;
    }

    /// <summary>
    /// データブロック1個 + EndMark の LZ4フレーム
    /// magic(4B) + FLG(1B) + BD(1B) + header_checksum(1B) + block_size(4B) + block_data(3B) + EndMark(4B) = 18バイト
    /// </summary>
    public static byte[] CreateLz4WithDataBlock()
    {
        var data = new byte[18];
        var span = data.AsSpan();
        var pos = 0;

        // magic: 0x184D2204 (LE)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x184D2204); pos += 4;

        // FLG byte: same as minimal
        data[pos] = 0x60; pos += 1;

        // BD byte: same as minimal
        data[pos] = 0x40; pos += 1;

        // header_checksum
        data[pos] = 0x82; pos += 1;

        // Data block: block_size=3 (LE)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 3); pos += 4;

        // block_data: 3 bytes of dummy data
        data[pos] = 0xAA; pos += 1;
        data[pos] = 0xBB; pos += 1;
        data[pos] = 0xCC; pos += 1;

        // EndMark (block_size=0)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0);

        return data;
    }
}
