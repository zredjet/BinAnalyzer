using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

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

    /// <summary>
    /// 64bit, ビッグエンディアン, ET_EXEC, EM_X86_64, プログラムヘッダー1個の最小ELFを生成する。
    /// ELFヘッダー(64) + プログラムヘッダー(56) = 120バイト
    /// </summary>
    public static byte[] CreateMinimalElf64BigEndian()
    {
        var data = new byte[120];
        var span = data.AsSpan();

        // === e_ident (16 bytes) — endianness-agnostic ===
        data[0] = 0x7F; data[1] = 0x45; data[2] = 0x4C; data[3] = 0x46;
        data[4] = 2;     // ei_class = ELFCLASS64
        data[5] = 2;     // ei_data = ELFDATA2MSB (big-endian)
        data[6] = 1;     // ei_version
        data[7] = 0;     // ei_osabi
        data[8] = 0;     // ei_abiversion
        var pos = 16;

        // === ELF64 header (48 bytes, big-endian) ===
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 2);       pos += 2;  // e_type = ET_EXEC
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 62);      pos += 2;  // e_machine = EM_X86_64
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1);       pos += 4;  // e_version
        BinaryPrimitives.WriteUInt64BigEndian(span[pos..], 0x400000); pos += 8; // e_entry
        BinaryPrimitives.WriteUInt64BigEndian(span[pos..], 64);      pos += 8;  // e_phoff
        BinaryPrimitives.WriteUInt64BigEndian(span[pos..], 0);       pos += 8;  // e_shoff
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0);       pos += 4;  // e_flags
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 64);      pos += 2;  // e_ehsize
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 56);      pos += 2;  // e_phentsize
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1);       pos += 2;  // e_phnum = 1
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 64);      pos += 2;  // e_shentsize
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0);       pos += 2;  // e_shnum = 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0);       pos += 2;  // e_shstrndx

        // === Program header (56 bytes, big-endian) ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1);       pos += 4;  // p_type = PT_LOAD
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 5);       pos += 4;  // p_flags = PF_R | PF_X
        BinaryPrimitives.WriteUInt64BigEndian(span[pos..], 0);       pos += 8;  // p_offset
        BinaryPrimitives.WriteUInt64BigEndian(span[pos..], 0x400000); pos += 8; // p_vaddr
        BinaryPrimitives.WriteUInt64BigEndian(span[pos..], 0x400000); pos += 8; // p_paddr
        BinaryPrimitives.WriteUInt64BigEndian(span[pos..], 120);     pos += 8;  // p_filesz
        BinaryPrimitives.WriteUInt64BigEndian(span[pos..], 120);     pos += 8;  // p_memsz
        BinaryPrimitives.WriteUInt64BigEndian(span[pos..], 0x200000); pos += 8; // p_align

        return data;
    }

    /// <summary>
    /// 32bit, ビッグエンディアン, ET_EXEC, EM_MIPS, プログラムヘッダー1個の最小ELFを生成する。
    /// ELFヘッダー(52) + プログラムヘッダー(32) = 84バイト
    /// </summary>
    public static byte[] CreateMinimalElf32BigEndian()
    {
        var data = new byte[84];
        var span = data.AsSpan();

        // === e_ident (16 bytes) ===
        data[0] = 0x7F; data[1] = 0x45; data[2] = 0x4C; data[3] = 0x46;
        data[4] = 1;     // ei_class = ELFCLASS32
        data[5] = 2;     // ei_data = ELFDATA2MSB (big-endian)
        data[6] = 1;     // ei_version
        data[7] = 0;     // ei_osabi
        data[8] = 0;     // ei_abiversion
        var pos = 16;

        // === ELF32 header (36 bytes, big-endian) ===
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 2);    pos += 2;  // e_type = ET_EXEC
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 8);    pos += 2;  // e_machine = EM_MIPS
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1);    pos += 4;  // e_version
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x400000); pos += 4; // e_entry
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 52);   pos += 4;  // e_phoff
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0);    pos += 4;  // e_shoff
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0);    pos += 4;  // e_flags
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 52);   pos += 2;  // e_ehsize
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 32);   pos += 2;  // e_phentsize
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1);    pos += 2;  // e_phnum = 1
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 40);   pos += 2;  // e_shentsize
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0);    pos += 2;  // e_shnum = 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0);    pos += 2;  // e_shstrndx

        // === Program header (32 bytes, big-endian) ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1);    pos += 4;  // p_type = PT_LOAD
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0);    pos += 4;  // p_offset
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x400000); pos += 4; // p_vaddr
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x400000); pos += 4; // p_paddr
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 84);   pos += 4;  // p_filesz
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 84);   pos += 4;  // p_memsz
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 5);    pos += 4;  // p_flags = PF_R | PF_X
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x10000); pos += 4; // p_align

        return data;
    }

    /// <summary>
    /// セクションを持つ 64 ビット リトルエンディアンの共有オブジェクト（REQ-188）。PT_INTERP のプログラムヘッダ、
    /// .interp・.note.gnu.build-id・.dynsym・.dynstr・.rela.dyn・.dynamic（DT_NEEDED libc.so.6）・zlib で圧縮した .debug_str（SHF_COMPRESSED）・.shstrtab を持つ。
    /// </summary>
    public static byte[] CreateElf64WithSections()
    {
        var shstr = new StringTable();
        var dynstr = new StringTable();
        var interp = Encoding.ASCII.GetBytes("/lib64/ld-linux-x86-64.so.2\0");

        // ノート: namesz 4 'GNU\0'、NT_GNU_BUILD_ID、記述子 20 バイト
        var note = new MemoryStream();
        var nw = new BinaryWriter(note);
        nw.Write(4u); nw.Write(20u); nw.Write(3u); nw.Write("GNU\0"u8);
        for (var i = 0; i < 20; i++) nw.Write((byte)(0xA0 + i));

        // .dynsym: 0 番（空）+ add（FUNC GLOBAL）+ counter（OBJECT WEAK）
        var addName = dynstr.Add("add");
        var counterName = dynstr.Add("counter");
        var libcName = dynstr.Add("libc.so.6");
        var dynsym = new MemoryStream();
        var sw = new BinaryWriter(dynsym);
        sw.Write(new byte[24]);
        sw.Write(addName); sw.Write((byte)0x12); sw.Write((byte)0); sw.Write((ushort)1); sw.Write(0x1000UL); sw.Write(16UL);
        sw.Write(counterName); sw.Write((byte)0x21); sw.Write((byte)2); sw.Write((ushort)0xFFF1); sw.Write(0x2000UL); sw.Write(4UL);

        // .rela.dyn: R_X86_64_GLOB_DAT（6）でシンボル 2 番
        var rela = new MemoryStream();
        var rw = new BinaryWriter(rela);
        rw.Write(0x3000UL); rw.Write((2UL << 32) | 6UL); rw.Write(-8L);

        // .dynamic: DT_NEEDED libc.so.6、DT_STRSZ、DT_NULL
        var dynamic = new MemoryStream();
        var dw = new BinaryWriter(dynamic);
        dw.Write(1L); dw.Write((ulong)libcName);
        dw.Write(10L); dw.Write((ulong)dynstr.Length);
        dw.Write(0L); dw.Write(0UL);

        // .debug_str: Elf64_Chdr（zlib）+ 圧縮データ
        var debugText = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("debug string ", 10)));
        var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(debugText);
        var debug = new MemoryStream();
        var cw = new BinaryWriter(debug);
        cw.Write(1u); cw.Write(0u); cw.Write((ulong)debugText.Length); cw.Write(1UL); cw.Write(compressed.ToArray());

        var sections = new List<(string Name, uint Type, ulong Flags, byte[] Data, uint Link, uint Info, ulong Align, ulong EntSize)>
        {
            (".interp", 1, 2, interp, 0, 0, 1, 0),
            (".note.gnu.build-id", 7, 2, note.ToArray(), 0, 0, 4, 0),
            (".dynsym", 11, 2, dynsym.ToArray(), 4, 1, 8, 24),
            (".dynstr", 3, 2, [], 0, 0, 1, 0),
            (".rela.dyn", 4, 2, rela.ToArray(), 3, 0, 8, 24),
            (".dynamic", 6, 3, dynamic.ToArray(), 4, 0, 8, 16),
            (".debug_str", 1, 0x830, debug.ToArray(), 0, 0, 8, 1),
            (".shstrtab", 3, 0, [], 0, 0, 1, 0),
        };
        foreach (var sec in sections) shstr.Add(sec.Name);
        sections[3] = sections[3] with { Data = dynstr.ToArray() };
        sections[7] = sections[7] with { Data = shstr.ToArray() };

        const int headerSize = 64, phdrSize = 56;
        var ms = new MemoryStream();
        ms.Write(new byte[headerSize + phdrSize]);
        var offsets = new List<long>();
        foreach (var sec in sections)
        {
            while (ms.Length % 8 != 0) ms.WriteByte(0);
            offsets.Add(ms.Length);
            ms.Write(sec.Data);
        }
        while (ms.Length % 8 != 0) ms.WriteByte(0);
        var shoff = ms.Length;
        var w = new BinaryWriter(ms);
        w.Write(new byte[64]);  // 0 番のセクションヘッダ
        for (var i = 0; i < sections.Count; i++)
        {
            var sec = sections[i];
            w.Write(shstr.Offset(sec.Name)); w.Write(sec.Type); w.Write(sec.Flags); w.Write(0UL);
            w.Write((ulong)offsets[i]); w.Write((ulong)sec.Data.Length); w.Write(sec.Link); w.Write(sec.Info);
            w.Write(sec.Align); w.Write(sec.EntSize);
        }

        var data = ms.ToArray();
        var span = data.AsSpan();
        new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F', 2, 1, 1, 0 }.CopyTo(span);
        BinaryPrimitives.WriteUInt16LittleEndian(span[16..], 3);     // ET_DYN
        BinaryPrimitives.WriteUInt16LittleEndian(span[18..], 62);    // EM_X86_64
        BinaryPrimitives.WriteUInt32LittleEndian(span[20..], 1);
        BinaryPrimitives.WriteUInt64LittleEndian(span[32..], headerSize);  // e_phoff
        BinaryPrimitives.WriteUInt64LittleEndian(span[40..], (ulong)shoff);
        BinaryPrimitives.WriteUInt16LittleEndian(span[52..], headerSize);
        BinaryPrimitives.WriteUInt16LittleEndian(span[54..], phdrSize);
        BinaryPrimitives.WriteUInt16LittleEndian(span[56..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(span[58..], 64);
        BinaryPrimitives.WriteUInt16LittleEndian(span[60..], (ushort)(sections.Count + 1));
        BinaryPrimitives.WriteUInt16LittleEndian(span[62..], (ushort)sections.Count);  // .shstrtab は最後
        // PT_INTERP
        var ph = span[headerSize..];
        BinaryPrimitives.WriteUInt32LittleEndian(ph, 3);
        BinaryPrimitives.WriteUInt32LittleEndian(ph[4..], 4);
        BinaryPrimitives.WriteUInt64LittleEndian(ph[8..], (ulong)offsets[0]);
        BinaryPrimitives.WriteUInt64LittleEndian(ph[32..], (ulong)interp.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(ph[40..], (ulong)interp.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(ph[48..], 1);
        return data;
    }

    /// <summary>NUL 終端の文字列の並び（先頭は空文字列）。</summary>
    private sealed class StringTable
    {
        private readonly MemoryStream _data = new();
        private readonly Dictionary<string, uint> _offsets = new() { [""] = 0 };

        public StringTable() => _data.WriteByte(0);

        public uint Add(string value)
        {
            if (_offsets.TryGetValue(value, out var existing))
                return existing;
            var offset = (uint)_data.Length;
            _data.Write(Encoding.ASCII.GetBytes(value + "\0"));
            _offsets[value] = offset;
            return offset;
        }

        public uint Offset(string value) => _offsets[value];

        public int Length => (int)_data.Length;

        public byte[] ToArray() => _data.ToArray();
    }
}
