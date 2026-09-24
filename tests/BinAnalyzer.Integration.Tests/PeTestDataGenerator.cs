using System.Buffers.Binary;
using System.Text;

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

    /// <summary>
    /// PE32+ の DLL（REQ-188）。.rdata（RVA 0x1000）にエクスポート表（gen.dll: Alpha・Beta）・インポート表（KERNEL32.dll の ExitProcess と序数 5）・
    /// デバッグディレクトリ（CodeView の C:\gen.pdb）、.reloc（RVA 0x2000）にベース再配置 1 ブロック、ファイルの末尾に証明書の表を置く。
    /// </summary>
    public static byte[] CreatePe32PlusDll()
    {
        var data = new byte[0x810];
        var s = data.AsSpan();
        WriteHeaders(s, lfanew: 0x80, machine: 0x8664, sections: 2, pe32Plus: true, characteristics: 0x2022);
        var opt = s[(0x80 + 24)..];
        BinaryPrimitives.WriteUInt32LittleEndian(opt[20..], 0x1000);          // base_of_code
        BinaryPrimitives.WriteUInt64LittleEndian(opt[24..], 0x180000000);     // image_base
        BinaryPrimitives.WriteUInt32LittleEndian(opt[56..], 0x3000);          // size_of_image
        BinaryPrimitives.WriteUInt16LittleEndian(opt[68..], 2);               // subsystem = WINDOWS_GUI
        BinaryPrimitives.WriteUInt16LittleEndian(opt[70..], 0x160);           // HIGH_ENTROPY_VA | DYNAMIC_BASE | NX_COMPAT
        var dirs = opt[112..];
        SetDirectory(dirs, 0, 0x1000, 0x60);   // エクスポート
        SetDirectory(dirs, 1, 0x1200, 40);     // インポート
        SetDirectory(dirs, 4, 0x800, 16);      // 証明書（ファイル上の位置）
        SetDirectory(dirs, 5, 0x2000, 12);     // ベース再配置
        SetDirectory(dirs, 6, 0x1300, 28);     // デバッグ
        SetDirectory(dirs, 12, 0x1260, 24);    // IAT
        var table = s[(0x80 + 24 + 240)..];
        WriteSection(table, ".rdata", 0x1000, 0x400, 0x200, 0x40000040);
        WriteSection(table[40..], ".reloc", 0x2000, 0x200, 0x600, 0x42000040);

        // .rdata（ファイル上の位置 = RVA − 0x1000 + 0x200）
        Span<byte> R(int rva) => data.AsSpan(rva - 0x1000 + 0x200);
        var ed = R(0x1000);
        BinaryPrimitives.WriteUInt32LittleEndian(ed[12..], 0x1100);   // name_rva
        BinaryPrimitives.WriteUInt32LittleEndian(ed[16..], 1);        // ordinal_base
        BinaryPrimitives.WriteUInt32LittleEndian(ed[20..], 2);        // number_of_functions
        BinaryPrimitives.WriteUInt32LittleEndian(ed[24..], 2);        // number_of_names
        BinaryPrimitives.WriteUInt32LittleEndian(ed[28..], 0x1040);
        BinaryPrimitives.WriteUInt32LittleEndian(ed[32..], 0x1048);
        BinaryPrimitives.WriteUInt32LittleEndian(ed[36..], 0x1050);
        BinaryPrimitives.WriteUInt32LittleEndian(R(0x1040), 0x1500);
        BinaryPrimitives.WriteUInt32LittleEndian(R(0x1044), 0x1510);
        BinaryPrimitives.WriteUInt32LittleEndian(R(0x1048), 0x1110);
        BinaryPrimitives.WriteUInt32LittleEndian(R(0x104C), 0x1118);
        BinaryPrimitives.WriteUInt16LittleEndian(R(0x1050), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(R(0x1052), 1);
        "gen.dll\0"u8.CopyTo(R(0x1100));
        "Alpha\0"u8.CopyTo(R(0x1110));
        "Beta\0"u8.CopyTo(R(0x1118));

        var id = R(0x1200);
        BinaryPrimitives.WriteUInt32LittleEndian(id, 0x1240);         // original_first_thunk
        BinaryPrimitives.WriteUInt32LittleEndian(id[12..], 0x1280);   // name
        BinaryPrimitives.WriteUInt32LittleEndian(id[16..], 0x1260);   // first_thunk
        foreach (var thunks in new[] { 0x1240, 0x1260 })
        {
            BinaryPrimitives.WriteUInt64LittleEndian(R(thunks), 0x1290);
            BinaryPrimitives.WriteUInt64LittleEndian(R(thunks + 8), 0x8000000000000005);
        }
        "KERNEL32.dll\0"u8.CopyTo(R(0x1280));
        BinaryPrimitives.WriteUInt16LittleEndian(R(0x1290), 0x123);
        "ExitProcess\0"u8.CopyTo(R(0x1292));

        var codeView = "RSDS"u8.ToArray().Concat(Enumerable.Range(1, 16).Select(i => (byte)i)).Concat(new byte[] { 1, 0, 0, 0 })
            .Concat(Encoding.UTF8.GetBytes("C:\\gen.pdb\0")).ToArray();
        var dd = R(0x1300);
        BinaryPrimitives.WriteUInt32LittleEndian(dd[12..], 2);                          // CODEVIEW
        BinaryPrimitives.WriteUInt32LittleEndian(dd[16..], (uint)codeView.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(dd[20..], 0x1320);
        BinaryPrimitives.WriteUInt32LittleEndian(dd[24..], 0x1320 - 0x1000 + 0x200);   // ファイル上の位置
        codeView.CopyTo(R(0x1320));

        // .reloc: ページ 0x1000 に DIR64 1 個 + 詰め物
        BinaryPrimitives.WriteUInt32LittleEndian(s[0x600..], 0x1000);
        BinaryPrimitives.WriteUInt32LittleEndian(s[0x604..], 12);
        BinaryPrimitives.WriteUInt16LittleEndian(s[0x608..], (10 << 12) | 0x010);
        BinaryPrimitives.WriteUInt16LittleEndian(s[0x60A..], 0);

        // 証明書: 13 バイト + 8 バイト境界までの詰め物
        BinaryPrimitives.WriteUInt32LittleEndian(s[0x800..], 13);
        BinaryPrimitives.WriteUInt16LittleEndian(s[0x804..], 0x0200);
        BinaryPrimitives.WriteUInt16LittleEndian(s[0x806..], 0x0002);
        new byte[] { 0x30, 0x82, 0x00, 0x00, 0x00 }.CopyTo(s[0x808..]);
        return data;
    }

    /// <summary>
    /// .NET の PE32 の DLL（REQ-188）。.text（RVA 0x2000）に CLR ヘッダ・メタデータのルート（'BSJB'、v4.0.30319、#~ と #Strings）・
    /// mscoree.dll の _CorDllMain のインポート（PE32 の 4 バイトのサンク）を置く。
    /// </summary>
    public static byte[] CreateManagedPe32()
    {
        var data = new byte[0x400];
        var s = data.AsSpan();
        WriteHeaders(s, lfanew: 0x80, machine: 0x14C, sections: 1, pe32Plus: false, characteristics: 0x2102);
        var opt = s[(0x80 + 24)..];
        BinaryPrimitives.WriteUInt32LittleEndian(opt[28..], 0x400000);   // image_base
        BinaryPrimitives.WriteUInt32LittleEndian(opt[56..], 0x4000);
        BinaryPrimitives.WriteUInt16LittleEndian(opt[68..], 3);
        var dirs = opt[96..];
        SetDirectory(dirs, 1, 0x2100, 40);
        SetDirectory(dirs, 14, 0x2000, 72);
        WriteSection(s[(0x80 + 24 + 224)..], ".text", 0x2000, 0x200, 0x200, 0x60000020);

        Span<byte> R(int rva) => data.AsSpan(rva - 0x2000 + 0x200);
        var clr = R(0x2000);
        BinaryPrimitives.WriteUInt32LittleEndian(clr, 72);
        BinaryPrimitives.WriteUInt16LittleEndian(clr[4..], 2);
        BinaryPrimitives.WriteUInt16LittleEndian(clr[6..], 5);
        BinaryPrimitives.WriteUInt32LittleEndian(clr[8..], 0x2048);     // メタデータの RVA
        BinaryPrimitives.WriteUInt32LittleEndian(clr[12..], 0x40);
        BinaryPrimitives.WriteUInt32LittleEndian(clr[16..], 1);         // ILONLY
        var md = R(0x2048);
        BinaryPrimitives.WriteUInt32LittleEndian(md, 0x424A5342);      // 'BSJB'
        BinaryPrimitives.WriteUInt16LittleEndian(md[4..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(md[6..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(md[12..], 12);
        "v4.0.30319\0\0"u8.CopyTo(md[16..]);
        BinaryPrimitives.WriteUInt16LittleEndian(md[30..], 2);         // ストリームの数
        BinaryPrimitives.WriteUInt32LittleEndian(md[32..], 0x6C);
        BinaryPrimitives.WriteUInt32LittleEndian(md[36..], 0x10);
        "#~\0\0"u8.CopyTo(md[40..]);
        BinaryPrimitives.WriteUInt32LittleEndian(md[44..], 0x7C);
        BinaryPrimitives.WriteUInt32LittleEndian(md[48..], 0x08);
        "#Strings\0\0\0\0"u8.CopyTo(md[52..]);

        var id = R(0x2100);
        BinaryPrimitives.WriteUInt32LittleEndian(id, 0x2130);
        BinaryPrimitives.WriteUInt32LittleEndian(id[12..], 0x2140);
        BinaryPrimitives.WriteUInt32LittleEndian(id[16..], 0x2138);
        BinaryPrimitives.WriteUInt32LittleEndian(R(0x2130), 0x2150);
        BinaryPrimitives.WriteUInt32LittleEndian(R(0x2138), 0x2150);
        "mscoree.dll\0"u8.CopyTo(R(0x2140));
        "\0\0_CorDllMain\0"u8.CopyTo(R(0x2150));
        return data;
    }

    private static void WriteHeaders(Span<byte> s, int lfanew, ushort machine, ushort sections, bool pe32Plus, ushort characteristics)
    {
        s[0] = (byte)'M'; s[1] = (byte)'Z';
        BinaryPrimitives.WriteUInt32LittleEndian(s[60..], (uint)lfanew);
        "This program cannot be run in DOS mode.\r\n$"u8.CopyTo(s[0x4E..]);
        "PE\0\0"u8.CopyTo(s[lfanew..]);
        var coff = s[(lfanew + 4)..];
        BinaryPrimitives.WriteUInt16LittleEndian(coff, machine);
        BinaryPrimitives.WriteUInt16LittleEndian(coff[2..], sections);
        BinaryPrimitives.WriteUInt16LittleEndian(coff[16..], (ushort)(pe32Plus ? 240 : 224));
        BinaryPrimitives.WriteUInt16LittleEndian(coff[18..], characteristics);
        var opt = s[(lfanew + 24)..];
        BinaryPrimitives.WriteUInt16LittleEndian(opt, (ushort)(pe32Plus ? 0x20B : 0x10B));
        BinaryPrimitives.WriteUInt32LittleEndian(opt[32..], 0x1000);   // section_alignment
        BinaryPrimitives.WriteUInt32LittleEndian(opt[36..], 0x200);    // file_alignment
        BinaryPrimitives.WriteUInt16LittleEndian(opt[40..], 6);        // major_os_version
        BinaryPrimitives.WriteUInt16LittleEndian(opt[48..], 6);        // major_subsystem_version
        BinaryPrimitives.WriteUInt32LittleEndian(opt[60..], 0x200);    // size_of_headers
        BinaryPrimitives.WriteUInt32LittleEndian(opt[(pe32Plus ? 108 : 92)..], 16);  // number_of_rva_and_sizes
    }

    private static void SetDirectory(Span<byte> directories, int index, uint rva, uint size)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(directories[(index * 8)..], rva);
        BinaryPrimitives.WriteUInt32LittleEndian(directories[(index * 8 + 4)..], size);
    }

    private static void WriteSection(Span<byte> header, string name, uint rva, uint size, uint rawPointer, uint characteristics)
    {
        Encoding.ASCII.GetBytes(name).CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], size);
        BinaryPrimitives.WriteUInt32LittleEndian(header[12..], rva);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], size);
        BinaryPrimitives.WriteUInt32LittleEndian(header[20..], rawPointer);
        BinaryPrimitives.WriteUInt32LittleEndian(header[36..], characteristics);
    }
}
