using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

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
    /// Mach-O 64bit with LC_BUILD_VERSION: magic(4B) + header_64_body(28B) + LC_BUILD_VERSION(cmd=0x32, cmdsize=32) = 64バイト
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

        // === load_command: LC_BUILD_VERSION (cmd=0x32, cmdsize=32) ===
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x32); pos += 4;  // cmd
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

    /// <summary>
    /// 2 スライスのユニバーサルバイナリ（REQ-188）。
    /// スライス 1（0x1000）: 64 ビット リトルエンディアンの arm64 実行ファイル（__TEXT,__text、シンボル 2 個、libSystem の LC_LOAD_DYLIB、
    /// __LINKEDIT、LC_BUILD_VERSION、識別子 com.example.gen とエンタイトルメントを持つコード署名）。
    /// スライス 2（0x2000）: 32 ビット ビッグエンディアンの PowerPC 実行ファイル（__TEXT,__text、シンボル 1 個）。
    /// </summary>
    public static byte[] CreateUniversalBinary()
    {
        var arm64 = CreateArm64Slice();
        var ppc = CreatePowerPcSlice();
        var data = new byte[0x2000 + ppc.Length];
        var s = data.AsSpan();
        BinaryPrimitives.WriteUInt32BigEndian(s, 0xCAFEBABE);
        BinaryPrimitives.WriteUInt32BigEndian(s[4..], 2);
        WriteFatArch(s[8..], 0x0100000C, 0, 0x1000, arm64.Length);
        WriteFatArch(s[28..], 18, 0, 0x2000, ppc.Length);
        arm64.CopyTo(s[0x1000..]);
        ppc.CopyTo(s[0x2000..]);
        return data;
    }

    private static void WriteFatArch(Span<byte> s, uint cpuType, uint cpuSubtype, int offset, int size)
    {
        BinaryPrimitives.WriteUInt32BigEndian(s, cpuType);
        BinaryPrimitives.WriteUInt32BigEndian(s[4..], cpuSubtype);
        BinaryPrimitives.WriteUInt32BigEndian(s[8..], (uint)offset);
        BinaryPrimitives.WriteUInt32BigEndian(s[12..], (uint)size);
        BinaryPrimitives.WriteUInt32BigEndian(s[16..], 12);
    }

    private static byte[] CreateArm64Slice()
    {
        const string identifier = "com.example.gen";
        const string entitlements = "<plist><dict><key>com.apple.security.get-task-allow</key><true/></dict></plist>";
        var signatureLength = CreateCodeSignature(identifier, entitlements, new byte[0x600]).Length;
        var data = new byte[0x600 + signatureLength];
        var s = data.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(s, 0xFEEDFACF);
        BinaryPrimitives.WriteUInt32LittleEndian(s[4..], 0x0100000C);
        BinaryPrimitives.WriteUInt32LittleEndian(s[12..], 2);          // MH_EXECUTE
        BinaryPrimitives.WriteUInt32LittleEndian(s[16..], 6);          // ncmds
        BinaryPrimitives.WriteUInt32LittleEndian(s[20..], 352);        // sizeofcmds
        BinaryPrimitives.WriteUInt32LittleEndian(s[24..], 0x00200085); // NOUNDEFS | DYLDLINK | TWOLEVEL | PIE

        var c = s[32..];
        // LC_SEGMENT_64 __TEXT + section __text
        BinaryPrimitives.WriteUInt32LittleEndian(c, 0x19);
        BinaryPrimitives.WriteUInt32LittleEndian(c[4..], 152);
        "__TEXT"u8.CopyTo(c[8..]);
        BinaryPrimitives.WriteUInt64LittleEndian(c[24..], 0x100000000);
        BinaryPrimitives.WriteUInt64LittleEndian(c[32..], 0x1000);
        BinaryPrimitives.WriteUInt64LittleEndian(c[48..], 0x500);
        BinaryPrimitives.WriteUInt32LittleEndian(c[56..], 5);          // maxprot = r-x
        BinaryPrimitives.WriteUInt32LittleEndian(c[60..], 5);
        BinaryPrimitives.WriteUInt32LittleEndian(c[64..], 1);          // nsects
        var sect = c[72..];
        "__text"u8.CopyTo(sect);
        "__TEXT"u8.CopyTo(sect[16..]);
        BinaryPrimitives.WriteUInt64LittleEndian(sect[32..], 0x100000400);
        BinaryPrimitives.WriteUInt64LittleEndian(sect[40..], 4);
        BinaryPrimitives.WriteUInt32LittleEndian(sect[48..], 0x400);
        BinaryPrimitives.WriteUInt32LittleEndian(sect[52..], 2);
        BinaryPrimitives.WriteUInt32LittleEndian(sect[64..], 0x80000400); // PURE_INSTRUCTIONS | SOME_INSTRUCTIONS
        c = c[152..];
        // LC_SEGMENT_64 __LINKEDIT（シンボル表・文字列表・コード署名を含む）
        BinaryPrimitives.WriteUInt32LittleEndian(c, 0x19);
        BinaryPrimitives.WriteUInt32LittleEndian(c[4..], 72);
        "__LINKEDIT"u8.CopyTo(c[8..]);
        BinaryPrimitives.WriteUInt64LittleEndian(c[24..], 0x100001000);
        BinaryPrimitives.WriteUInt64LittleEndian(c[32..], 0x1000);
        BinaryPrimitives.WriteUInt64LittleEndian(c[40..], 0x500);
        BinaryPrimitives.WriteUInt64LittleEndian(c[48..], (ulong)(data.Length - 0x500));
        BinaryPrimitives.WriteUInt32LittleEndian(c[56..], 1);          // maxprot = r--
        BinaryPrimitives.WriteUInt32LittleEndian(c[60..], 1);
        c = c[72..];
        // LC_SYMTAB
        BinaryPrimitives.WriteUInt32LittleEndian(c, 0x2);
        BinaryPrimitives.WriteUInt32LittleEndian(c[4..], 24);
        BinaryPrimitives.WriteUInt32LittleEndian(c[8..], 0x500);
        BinaryPrimitives.WriteUInt32LittleEndian(c[12..], 2);
        BinaryPrimitives.WriteUInt32LittleEndian(c[16..], 0x520);
        BinaryPrimitives.WriteUInt32LittleEndian(c[20..], 16);
        c = c[24..];
        // LC_LOAD_DYLIB
        BinaryPrimitives.WriteUInt32LittleEndian(c, 0xC);
        BinaryPrimitives.WriteUInt32LittleEndian(c[4..], 56);
        BinaryPrimitives.WriteUInt32LittleEndian(c[8..], 24);
        BinaryPrimitives.WriteUInt32LittleEndian(c[12..], 2);
        BinaryPrimitives.WriteUInt32LittleEndian(c[16..], 1356u << 16);
        BinaryPrimitives.WriteUInt32LittleEndian(c[20..], 1u << 16);
        "/usr/lib/libSystem.B.dylib"u8.CopyTo(c[24..]);
        c = c[56..];
        // LC_BUILD_VERSION
        BinaryPrimitives.WriteUInt32LittleEndian(c, 0x32);
        BinaryPrimitives.WriteUInt32LittleEndian(c[4..], 32);
        BinaryPrimitives.WriteUInt32LittleEndian(c[8..], 1);           // macOS
        BinaryPrimitives.WriteUInt32LittleEndian(c[12..], 14u << 16);  // 14.0.0
        BinaryPrimitives.WriteUInt32LittleEndian(c[16..], 26u << 16);  // 26.0.0
        BinaryPrimitives.WriteUInt32LittleEndian(c[20..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(c[24..], 3);          // ld
        BinaryPrimitives.WriteUInt32LittleEndian(c[28..], 1230u << 16);
        c = c[32..];
        // LC_CODE_SIGNATURE
        BinaryPrimitives.WriteUInt32LittleEndian(c, 0x1D);
        BinaryPrimitives.WriteUInt32LittleEndian(c[4..], 16);
        BinaryPrimitives.WriteUInt32LittleEndian(c[8..], 0x600);
        BinaryPrimitives.WriteUInt32LittleEndian(c[12..], (uint)signatureLength);

        new byte[] { 0xC0, 0x03, 0x5F, 0xD6 }.CopyTo(s[0x400..]);     // ret
        // nlist_64: _main（N_SECT | N_EXT、セクション 1）、_helper（未定義、libSystem）
        BinaryPrimitives.WriteUInt32LittleEndian(s[0x500..], 1);
        s[0x504] = 0x0F; s[0x505] = 1;
        BinaryPrimitives.WriteUInt64LittleEndian(s[0x508..], 0x100000400);
        BinaryPrimitives.WriteUInt32LittleEndian(s[0x510..], 7);
        s[0x514] = 0x01;
        BinaryPrimitives.WriteUInt16LittleEndian(s[0x516..], 0x0100);
        "\0_main\0_helper\0"u8.CopyTo(s[0x520..]);
        CreateCodeSignature(identifier, entitlements, data.AsSpan(0, 0x600)).CopyTo(s[0x600..]);
        return data;
    }

    /// <summary>
    /// SuperBlob（ビッグエンディアン）: CodeDirectory（版 0x20400、code_limit までのページのハッシュと、エンタイトルメントの特別スロットのハッシュ）
    /// + エンタイトルメント。<paramref name="code"/> は署名の直前までのスライスの中身（ページのハッシュの対象）。
    /// </summary>
    private static byte[] CreateCodeSignature(string identifier, string entitlements, ReadOnlySpan<byte> code)
    {
        const int specialSlots = 5, hashSize = 32, pageSize = 4096;
        var codeSlots = (code.Length + pageSize - 1) / pageSize;
        var ident = Encoding.ASCII.GetBytes(identifier + "\0");
        var hashOffset = 88 + ident.Length + specialSlots * hashSize;
        var cdLength = hashOffset + codeSlots * hashSize;
        var ent = Encoding.UTF8.GetBytes(entitlements);
        var entLength = 8 + ent.Length;
        const int indexEnd = 12 + 2 * 8;
        var data = new byte[indexEnd + cdLength + entLength];
        var s = data.AsSpan();
        BinaryPrimitives.WriteUInt32BigEndian(s, 0xFADE0CC0);
        BinaryPrimitives.WriteUInt32BigEndian(s[4..], (uint)data.Length);
        BinaryPrimitives.WriteUInt32BigEndian(s[8..], 2);
        BinaryPrimitives.WriteUInt32BigEndian(s[12..], 0);             // CSSLOT_CODEDIRECTORY
        BinaryPrimitives.WriteUInt32BigEndian(s[16..], indexEnd);
        BinaryPrimitives.WriteUInt32BigEndian(s[20..], 5);             // CSSLOT_ENTITLEMENTS
        BinaryPrimitives.WriteUInt32BigEndian(s[24..], (uint)(indexEnd + cdLength));

        var e = s[(indexEnd + cdLength)..];
        BinaryPrimitives.WriteUInt32BigEndian(e, 0xFADE7171);
        BinaryPrimitives.WriteUInt32BigEndian(e[4..], (uint)entLength);
        ent.CopyTo(e[8..]);

        var cd = s[indexEnd..];
        BinaryPrimitives.WriteUInt32BigEndian(cd, 0xFADE0C02);
        BinaryPrimitives.WriteUInt32BigEndian(cd[4..], (uint)cdLength);
        BinaryPrimitives.WriteUInt32BigEndian(cd[8..], 0x20400);
        BinaryPrimitives.WriteUInt32BigEndian(cd[12..], 0x2);          // adhoc
        BinaryPrimitives.WriteUInt32BigEndian(cd[16..], (uint)hashOffset);
        BinaryPrimitives.WriteUInt32BigEndian(cd[20..], 88);           // ident_offset
        BinaryPrimitives.WriteUInt32BigEndian(cd[24..], specialSlots);
        BinaryPrimitives.WriteUInt32BigEndian(cd[28..], (uint)codeSlots);
        BinaryPrimitives.WriteUInt32BigEndian(cd[32..], (uint)code.Length);  // code_limit
        cd[36] = hashSize; cd[37] = 2; cd[39] = 12;                    // SHA-256、ページ 4 KiB
        ident.CopyTo(cd[88..]);
        // 特別スロット −5（エンタイトルメント）。−1〜−4 は対象が無いので 0
        SHA256.HashData(e[..entLength], cd[(hashOffset - specialSlots * hashSize)..]);
        for (var i = 0; i < codeSlots; i++)
        {
            var page = code[(i * pageSize)..Math.Min(code.Length, (i + 1) * pageSize)];
            SHA256.HashData(page, cd[(hashOffset + i * hashSize)..]);
        }
        return data;
    }

    private static byte[] CreatePowerPcSlice()
    {
        var data = new byte[0x314];
        var s = data.AsSpan();
        BinaryPrimitives.WriteUInt32BigEndian(s, 0xFEEDFACE);
        BinaryPrimitives.WriteUInt32BigEndian(s[4..], 18);             // CPU_TYPE_POWERPC
        BinaryPrimitives.WriteUInt32BigEndian(s[12..], 2);
        BinaryPrimitives.WriteUInt32BigEndian(s[16..], 2);
        BinaryPrimitives.WriteUInt32BigEndian(s[20..], 148);
        var c = s[28..];
        BinaryPrimitives.WriteUInt32BigEndian(c, 0x1);                 // LC_SEGMENT
        BinaryPrimitives.WriteUInt32BigEndian(c[4..], 124);
        "__TEXT"u8.CopyTo(c[8..]);
        BinaryPrimitives.WriteUInt32BigEndian(c[24..], 0x1000);
        BinaryPrimitives.WriteUInt32BigEndian(c[28..], 0x1000);
        BinaryPrimitives.WriteUInt32BigEndian(c[36..], 0x314);
        BinaryPrimitives.WriteUInt32BigEndian(c[40..], 5);
        BinaryPrimitives.WriteUInt32BigEndian(c[44..], 5);
        BinaryPrimitives.WriteUInt32BigEndian(c[48..], 1);
        var sect = c[56..];
        "__text"u8.CopyTo(sect);
        "__TEXT"u8.CopyTo(sect[16..]);
        BinaryPrimitives.WriteUInt32BigEndian(sect[32..], 0x1200);
        BinaryPrimitives.WriteUInt32BigEndian(sect[36..], 4);
        BinaryPrimitives.WriteUInt32BigEndian(sect[40..], 0x200);
        BinaryPrimitives.WriteUInt32BigEndian(sect[44..], 2);
        BinaryPrimitives.WriteUInt32BigEndian(sect[56..], 0x80000400);
        c = c[124..];
        BinaryPrimitives.WriteUInt32BigEndian(c, 0x2);                 // LC_SYMTAB
        BinaryPrimitives.WriteUInt32BigEndian(c[4..], 24);
        BinaryPrimitives.WriteUInt32BigEndian(c[8..], 0x300);
        BinaryPrimitives.WriteUInt32BigEndian(c[12..], 1);
        BinaryPrimitives.WriteUInt32BigEndian(c[16..], 0x30C);
        BinaryPrimitives.WriteUInt32BigEndian(c[20..], 8);
        new byte[] { 0x4E, 0x80, 0x00, 0x20 }.CopyTo(s[0x200..]);     // blr
        BinaryPrimitives.WriteUInt32BigEndian(s[0x300..], 1);
        s[0x304] = 0x0F; s[0x305] = 1;
        BinaryPrimitives.WriteUInt32BigEndian(s[0x308..], 0x1200);
        "\0_start\0"u8.CopyTo(s[0x30C..]);
        return data;
    }
}
