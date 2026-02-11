using System.Buffers.Binary;

namespace BinAnalyzer.Integration.Tests;

public static class MachoTestDataGenerator
{
    /// <summary>
    /// 最小Mach-O 64bitファイル: magic(4B) + mach_header_64_body(28B) + 1 load_command(UUID, 24B) = 56バイト
    /// 0xFEEDFACF magic, CPU_TYPE_ARM64, MH_EXECUTE, 1 LC_UUID
    /// </summary>
    public static byte[] CreateMinimalMacho64()
    {
        var data = new byte[56];
        var span = data.AsSpan();
        var pos = 0;

        // magic: 0xFEEDFACF (64-bit, little-endian)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0xFEEDFACF); pos += 4;

        // === mach_header_64_body (switch on magic) ===
        // cputype: CPU_TYPE_ARM64 = 0x0100000C = 16777228
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 16777228); pos += 4;

        // cpusubtype: CPU_SUBTYPE_ARM64_ALL = 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;

        // filetype: MH_EXECUTE = 2
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 2); pos += 4;

        // ncmds: 1
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1); pos += 4;

        // sizeofcmds: 24 (UUID command size)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 24); pos += 4;

        // flags: MH_PIE (0x200000) | MH_TWOLEVEL (0x80) | MH_DYLDLINK (0x04)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x200084); pos += 4;

        // reserved: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;

        // === load_command: LC_UUID (cmd=27, cmdsize=24) ===
        // cmd: 27 (LC_UUID)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 27); pos += 4;

        // cmdsize: 24
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 24); pos += 4;

        // body (uuid_body): 16 bytes UUID
        data[pos] = 0x01; data[pos + 1] = 0x02; data[pos + 2] = 0x03; data[pos + 3] = 0x04;
        data[pos + 4] = 0x05; data[pos + 5] = 0x06; data[pos + 6] = 0x07; data[pos + 7] = 0x08;
        data[pos + 8] = 0x09; data[pos + 9] = 0x0A; data[pos + 10] = 0x0B; data[pos + 11] = 0x0C;
        data[pos + 12] = 0x0D; data[pos + 13] = 0x0E; data[pos + 14] = 0x0F; data[pos + 15] = 0x10;

        return data;
    }

    /// <summary>
    /// Mach-O 64bit with LC_BUILD_VERSION: magic(4B) + header_64_body(28B) + LC_BUILD_VERSION(cmd=44, cmdsize=32) = 64バイト
    /// ntools=1, tool=3(ld), version=0x003C0600 (60.6.0)
    /// </summary>
    public static byte[] CreateMacho64WithBuildVersion()
    {
        var data = new byte[64];
        var span = data.AsSpan();
        var pos = 0;

        // magic: 0xFEEDFACF (64-bit, little-endian)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0xFEEDFACF); pos += 4;

        // === mach_header_64_body ===
        // cputype: CPU_TYPE_ARM64
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 16777228); pos += 4;
        // cpusubtype
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        // filetype: MH_EXECUTE = 2
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 2); pos += 4;
        // ncmds: 1
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1); pos += 4;
        // sizeofcmds: 32 (BUILD_VERSION command size)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 32); pos += 4;
        // flags
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x200084); pos += 4;
        // reserved
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;

        // === load_command: LC_BUILD_VERSION (cmd=44, cmdsize=32) ===
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 44); pos += 4;  // cmd
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 32); pos += 4;  // cmdsize

        // build_version_body: platform(4) + minos(4) + sdk(4) + ntools(4) + tool_entry(8) = 24
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1); pos += 4;   // platform: MACOS
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x000D0000); pos += 4; // minos: 13.0.0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x000E0000); pos += 4; // sdk: 14.0.0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1); pos += 4;   // ntools: 1

        // build_tool_entry: tool=3 (ld), version=0x003C0600
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 3); pos += 4;   // tool: ld
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x003C0600);    // version

        return data;
    }
}
