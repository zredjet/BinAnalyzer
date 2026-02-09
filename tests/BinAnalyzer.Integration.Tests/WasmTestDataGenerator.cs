using System.Buffers.Binary;

namespace BinAnalyzer.Integration.Tests;

public static class WasmTestDataGenerator
{
    /// <summary>
    /// 最小WASMバイナリ: magic(4B) + version(4B) + 1 section(section_id(1B) + section_size(1B LEB128) + section_data(4B)) = 14バイト
    /// Type section (id=1) を1つ含む
    /// </summary>
    public static byte[] CreateMinimalWasm()
    {
        var data = new byte[14];
        var span = data.AsSpan();
        var pos = 0;

        // magic: \0asm
        data[0] = 0x00;
        data[1] = 0x61; // 'a'
        data[2] = 0x73; // 's'
        data[3] = 0x6D; // 'm'
        pos = 4;

        // version: 1 (uint32 LE)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1); pos += 4;

        // section: type section (id=1)
        data[pos] = 1; pos += 1; // section_id = type

        // section_size: 4 bytes (LEB128, value 4 fits in 1 byte)
        data[pos] = 0x04; pos += 1;

        // section_data: 4 dummy bytes
        data[pos] = 0x01; pos += 1; // count = 1
        data[pos] = 0x60; pos += 1; // func type marker
        data[pos] = 0x00; pos += 1; // 0 params
        data[pos] = 0x00; pos += 1; // 0 results

        return data;
    }
}
