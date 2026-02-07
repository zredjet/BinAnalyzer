using System.Buffers.Binary;

namespace BinAnalyzer.Integration.Tests;

public static class PeTestDataGenerator
{
    /// <summary>
    /// 最小PEファイル(PE32+): DOS header(64B) + PE signature(4B) + COFF header(20B) +
    /// PE32+ optional header(112B + 2 data dirs * 8B = 128B) + 1 section header(40B) = 256バイト
    /// e_lfanew=64, AMD64, 1セクション
    /// </summary>
    public static byte[] CreateMinimalPe()
    {
        var data = new byte[256];
        var span = data.AsSpan();
        var pos = 0;

        // === DOS header (64 bytes) ===
        // e_magic: "MZ"
        data[0] = 0x4D; data[1] = 0x5A;
        pos = 2;

        // e_cblp through e_res2 (56 bytes of DOS header fields)
        // e_cblp: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // e_cp: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // e_crlc: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // e_cparhdr: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // e_minalloc: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // e_maxalloc: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // e_ss: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // e_sp: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // e_csum: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // e_ip: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // e_cs: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // e_lfarlc: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // e_ovno: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // e_res: 8 bytes
        pos += 8;
        // e_oemid: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // e_oeminfo: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // e_res2: 20 bytes
        pos += 20;
        // e_lfanew: 64 (PE header starts right after DOS header)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 64); pos += 4;

        // === PE signature at offset 64 (4 bytes) ===
        data[pos] = 0x50; // 'P'
        data[pos + 1] = 0x45; // 'E'
        data[pos + 2] = 0x00;
        data[pos + 3] = 0x00;
        pos += 4;

        // === COFF header (20 bytes) ===
        // machine: 0x8664 (AMD64)
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0x8664); pos += 2;
        // number_of_sections: 1
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 1); pos += 2;
        // time_date_stamp: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        // pointer_to_symbol_table: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        // number_of_symbols: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        // size_of_optional_header: 128
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 128); pos += 2;
        // characteristics: 0x0022 (EXECUTABLE_IMAGE | LARGE_ADDRESS_AWARE)
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0x0022); pos += 2;

        // === PE32+ optional header (128 bytes) ===
        // magic: 0x20B (PE32+)
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0x20B); pos += 2;
        // major_linker_version: 14
        data[pos] = 14; pos += 1;
        // minor_linker_version: 0
        data[pos] = 0; pos += 1;
        // size_of_code: 0x200
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x200); pos += 4;
        // size_of_initialized_data: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        // size_of_uninitialized_data: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        // address_of_entry_point: 0x1000
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x1000); pos += 4;
        // base_of_code: 0x1000
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x1000); pos += 4;
        // image_base: 0x140000000
        BinaryPrimitives.WriteUInt64LittleEndian(span[pos..], 0x140000000); pos += 8;
        // section_alignment: 0x1000
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x1000); pos += 4;
        // file_alignment: 0x200
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x200); pos += 4;
        // major_os_version: 6
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 6); pos += 2;
        // minor_os_version: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // major_image_version: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // minor_image_version: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // major_subsystem_version: 6
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 6); pos += 2;
        // minor_subsystem_version: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // win32_version_value: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        // size_of_image: 0x2000
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x2000); pos += 4;
        // size_of_headers: 0x200
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x200); pos += 4;
        // checksum: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        // subsystem: 3 (IMAGE_SUBSYSTEM_WINDOWS_CUI)
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 3); pos += 2;
        // dll_characteristics: 0x8160 (DYNAMIC_BASE | NX_COMPAT | TERMINAL_SERVER_AWARE)
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0x8160); pos += 2;
        // size_of_stack_reserve: 0x100000
        BinaryPrimitives.WriteUInt64LittleEndian(span[pos..], 0x100000); pos += 8;
        // size_of_stack_commit: 0x1000
        BinaryPrimitives.WriteUInt64LittleEndian(span[pos..], 0x1000); pos += 8;
        // size_of_heap_reserve: 0x100000
        BinaryPrimitives.WriteUInt64LittleEndian(span[pos..], 0x100000); pos += 8;
        // size_of_heap_commit: 0x1000
        BinaryPrimitives.WriteUInt64LittleEndian(span[pos..], 0x1000); pos += 8;
        // loader_flags: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        // number_of_rva_and_sizes: 2
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 2); pos += 4;

        // data_directories: 2 entries (8 bytes each)
        // Export directory: 0, 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        // Import directory: 0, 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;

        // === Section header (40 bytes) ===
        // name: ".text\0\0\0"
        data[pos] = 0x2E; // '.'
        data[pos + 1] = 0x74; // 't'
        data[pos + 2] = 0x65; // 'e'
        data[pos + 3] = 0x78; // 'x'
        data[pos + 4] = 0x74; // 't'
        pos += 8;

        // virtual_size: 0x100
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x100); pos += 4;
        // virtual_address: 0x1000
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x1000); pos += 4;
        // size_of_raw_data: 0x200
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x200); pos += 4;
        // pointer_to_raw_data: 0x200
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x200); pos += 4;
        // pointer_to_relocations: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        // pointer_to_linenumbers: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        // number_of_relocations: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // number_of_linenumbers: 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;
        // characteristics: 0x60000020 (CNT_CODE | MEM_EXECUTE | MEM_READ)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x60000020);

        return data;
    }
}
