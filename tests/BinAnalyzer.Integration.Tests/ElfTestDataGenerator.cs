using System.Buffers.Binary;

namespace BinAnalyzer.Integration.Tests;

public static class ElfTestDataGenerator
{
    /// <summary>
    /// 64bit, リトルエンディアン, ET_EXEC, EM_X86_64, プログラムヘッダー1個の最小ELFを生成する。
    /// ELFヘッダー(64) + プログラムヘッダー(56) = 120バイト
    /// </summary>
    public static byte[] CreateMinimalElf64()
    {
        var data = new byte[120];
        var span = data.AsSpan();
        var pos = 0;

        // === e_ident (16 bytes) ===
        data[0] = 0x7F; // magic
        data[1] = 0x45; // 'E'
        data[2] = 0x4C; // 'L'
        data[3] = 0x46; // 'F'
        data[4] = 2;     // ei_class = ELFCLASS64
        data[5] = 1;     // ei_data = ELFDATA2LSB
        data[6] = 1;     // ei_version = EV_CURRENT
        data[7] = 0;     // ei_osabi = ELFOSABI_NONE
        data[8] = 0;     // ei_abiversion
        // bytes 9-15: padding (zero)
        pos = 16;

        // === ELF64 header (48 bytes) ===
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 2);      pos += 2;  // e_type = ET_EXEC
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 62);     pos += 2;  // e_machine = EM_X86_64
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1);      pos += 4;  // e_version
        BinaryPrimitives.WriteUInt64LittleEndian(span[pos..], 0x400000); pos += 8; // e_entry
        BinaryPrimitives.WriteUInt64LittleEndian(span[pos..], 64);     pos += 8;  // e_phoff (right after header)
        BinaryPrimitives.WriteUInt64LittleEndian(span[pos..], 0);      pos += 8;  // e_shoff (no section headers)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0);      pos += 4;  // e_flags
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 64);     pos += 2;  // e_ehsize
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 56);     pos += 2;  // e_phentsize
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 1);      pos += 2;  // e_phnum = 1
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 64);     pos += 2;  // e_shentsize
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0);      pos += 2;  // e_shnum = 0
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0);      pos += 2;  // e_shstrndx

        // === Program header (56 bytes) ===
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1);      pos += 4;  // p_type = PT_LOAD
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 5);      pos += 4;  // p_flags = PF_R | PF_X
        BinaryPrimitives.WriteUInt64LittleEndian(span[pos..], 0);      pos += 8;  // p_offset
        BinaryPrimitives.WriteUInt64LittleEndian(span[pos..], 0x400000); pos += 8; // p_vaddr
        BinaryPrimitives.WriteUInt64LittleEndian(span[pos..], 0x400000); pos += 8; // p_paddr
        BinaryPrimitives.WriteUInt64LittleEndian(span[pos..], 120);    pos += 8;  // p_filesz
        BinaryPrimitives.WriteUInt64LittleEndian(span[pos..], 120);    pos += 8;  // p_memsz
        BinaryPrimitives.WriteUInt64LittleEndian(span[pos..], 0x200000); pos += 8; // p_align

        return data;
    }
}
