namespace BinAnalyzer.Integration.Tests;

/// <summary>
/// FAT12/16/32ファイルシステムイメージのテストデータを生成するヘルパー。
/// </summary>
public static class FatTestDataGenerator
{
    private static void WriteUInt16LE(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static void WriteUInt32LE(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteAscii(byte[] buffer, int offset, string text, int size)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(text);
        Array.Copy(bytes, 0, buffer, offset, Math.Min(bytes.Length, size));
        for (int i = bytes.Length; i < size; i++)
            buffer[offset + i] = 0x20; // pad with spaces
    }

    private static void WriteDirEntry(byte[] buffer, int offset,
        string filename, string extension, byte attributes,
        ushort creationTime, ushort creationDate, ushort lastAccessDate,
        ushort modificationTime, ushort modificationDate,
        ushort firstClusterLow, uint fileSize)
    {
        WriteAscii(buffer, offset, filename, 8);
        WriteAscii(buffer, offset + 8, extension, 3);
        buffer[offset + 11] = attributes;
        buffer[offset + 12] = 0; // reserved_nt
        buffer[offset + 13] = 0; // creation_time_tenths
        WriteUInt16LE(buffer, offset + 14, creationTime);
        WriteUInt16LE(buffer, offset + 16, creationDate);
        WriteUInt16LE(buffer, offset + 18, lastAccessDate);
        WriteUInt16LE(buffer, offset + 20, 0); // first_cluster_high
        WriteUInt16LE(buffer, offset + 22, modificationTime);
        WriteUInt16LE(buffer, offset + 24, modificationDate);
        WriteUInt16LE(buffer, offset + 26, firstClusterLow);
        WriteUInt32LE(buffer, offset + 28, fileSize);
    }

    /// <summary>
    /// 最小FAT16イメージ（20セクタ = 10240バイト）。
    /// ブートセクタ + FAT×2 + ルートディレクトリ（ボリュームラベル + ファイル + ディレクトリ）。
    /// </summary>
    /// <remarks>
    /// BPBパラメータ:
    ///   bytes_per_sector=512, sectors_per_cluster=1, reserved=1,
    ///   num_fats=2, root_entry_count=16, fat_size_16=1, total_sectors_16=20
    /// ルートディレクトリオフセット: (1 + 2*1) * 512 = 1536
    /// タイムスタンプ: 2024-06-15 10:30:00
    ///   modification_date=0x58CF, modification_time=0x53C0
    /// </remarks>
    public static byte[] CreateMinimalFat16Image()
    {
        var image = new byte[20 * 512]; // 10240 bytes

        // === Boot sector (sector 0, offset 0) ===
        // Jump instruction
        image[0] = 0xEB; image[1] = 0x3C; image[2] = 0x90;
        // OEM name
        WriteAscii(image, 3, "MSDOS5.0", 8);
        // Common BPB
        WriteUInt16LE(image, 11, 512);   // bytes_per_sector
        image[13] = 1;                    // sectors_per_cluster
        WriteUInt16LE(image, 14, 1);     // reserved_sector_count
        image[16] = 2;                    // num_fats
        WriteUInt16LE(image, 17, 16);    // root_entry_count
        WriteUInt16LE(image, 19, 20);    // total_sectors_16
        image[21] = 0xF8;                // media_type (FIXED_DISK)
        WriteUInt16LE(image, 22, 1);     // fat_size_16
        WriteUInt16LE(image, 24, 32);    // sectors_per_track
        WriteUInt16LE(image, 26, 64);    // num_heads
        WriteUInt32LE(image, 28, 0);     // hidden_sectors
        WriteUInt32LE(image, 32, 0);     // total_sectors_32

        // FAT16 extended BPB (offset 36)
        image[36] = 0x80;                         // drive_number
        image[37] = 0x00;                         // reserved1
        image[38] = 0x29;                         // boot_signature
        WriteUInt32LE(image, 39, 0x12345678);     // volume_id
        WriteAscii(image, 43, "TEST", 11);        // volume_label
        WriteAscii(image, 54, "FAT16", 8);        // fs_type_string
        // boot_code (448 bytes): zeros (already initialized)
        // Boot signature word
        image[510] = 0x55;
        image[511] = 0xAA;

        // === FAT1 (sector 1, offset 512) ===
        WriteUInt16LE(image, 512, 0xFFF8);  // Entry 0: media byte
        WriteUInt16LE(image, 514, 0xFFFF);  // Entry 1: end of chain
        WriteUInt16LE(image, 516, 0xFFFF);  // Entry 2: HELLO.TXT (single cluster)
        WriteUInt16LE(image, 518, 0xFFFF);  // Entry 3: SUBDIR (single cluster)

        // === FAT2 (sector 2, offset 1024) — mirror of FAT1 ===
        Array.Copy(image, 512, image, 1024, 512);

        // === Root directory (sector 3, offset 1536) ===
        // DOSタイムスタンプ: 2024-06-15 10:30:00
        // date = (44 << 9) | (6 << 5) | 15 = 0x58CF
        // time = (10 << 11) | (30 << 5) | 0 = 0x53C0
        const ushort dosDate = 0x58CF;
        const ushort dosTime = 0x53C0;
        int rootOffset = 1536;

        // Entry 1: Volume label
        WriteDirEntry(image, rootOffset, "TEST", "   ", 0x08,
            dosTime, dosDate, dosDate, dosTime, dosDate, 0, 0);

        // Entry 2: HELLO.TXT (ARCHIVE)
        WriteDirEntry(image, rootOffset + 32, "HELLO", "TXT", 0x20,
            dosTime, dosDate, dosDate, dosTime, dosDate, 2, 13);

        // Entry 3: SUBDIR (DIRECTORY)
        WriteDirEntry(image, rootOffset + 64, "SUBDIR", "   ", 0x10,
            dosTime, dosDate, dosDate, dosTime, dosDate, 3, 0);

        return image;
    }

    /// <summary>
    /// 最小の FAT32 イメージ（8 セクタ = 4096 バイト、REQ-188 で FSInfo・FAT・ルートディレクトリのクラスタを足した）。
    /// セクタ 0 = ブートセクタ（root_entry_count = 0、fat_size_16 = 0、fat_size_32 = 1、root_cluster = 2、fs_info_sector = 1）、
    /// セクタ 1 = FSInfo、セクタ 2 = FAT（クラスタ 2 = 連鎖の終わり）、セクタ 3 = クラスタ 2（ルートディレクトリ: ボリュームラベルのみ）。
    /// クラスタ数が FAT32 の下限（65525）より少ないので、macOS はマウントしない（Linux は BPB の形で FAT32 として読む）。
    /// </summary>
    public static byte[] CreateMinimalFat32Image()
    {
        const int bytesPerSector = 512;
        var image = new byte[bytesPerSector * 8];
        image[0] = 0xEB; image[1] = 0x58; image[2] = 0x90;
        WriteAscii(image, 3, "MSWIN4.1", 8);
        WriteUInt16LE(image, 11, bytesPerSector);
        image[13] = 1;                          // sectors_per_cluster
        WriteUInt16LE(image, 14, 2);            // reserved_sector_count（ブートセクタ + FSInfo）
        image[16] = 1;                          // num_fats
        WriteUInt16LE(image, 17, 0);            // root_entry_count
        WriteUInt16LE(image, 19, 8);            // total_sectors_16
        image[21] = 0xF8;
        WriteUInt16LE(image, 22, 0);            // fat_size_16
        WriteUInt16LE(image, 24, 63);
        WriteUInt16LE(image, 26, 255);
        WriteUInt32LE(image, 36, 1);            // fat_size_32
        WriteUInt32LE(image, 44, 2);            // root_cluster
        WriteUInt16LE(image, 48, 1);            // fs_info_sector
        WriteUInt16LE(image, 50, 0);            // backup_boot_sector（無し）
        image[64] = 0x80;
        image[66] = 0x29;
        WriteUInt32LE(image, 67, 0x12345678);
        WriteAscii(image, 71, "FAT32VOL", 11);
        WriteAscii(image, 82, "FAT32", 8);
        WriteUInt16LE(image, 510, 0xAA55);

        var fsinfo = bytesPerSector;
        WriteUInt32LE(image, fsinfo, 0x41615252);
        WriteUInt32LE(image, fsinfo + 484, 0x61417272);
        WriteUInt32LE(image, fsinfo + 488, 4);  // free_count
        WriteUInt32LE(image, fsinfo + 492, 3);  // next_free
        WriteUInt32LE(image, fsinfo + 508, 0xAA550000);

        var fat = bytesPerSector * 2;
        WriteUInt32LE(image, fat, 0x0FFFFFF8);
        WriteUInt32LE(image, fat + 4, 0x0FFFFFFF);
        WriteUInt32LE(image, fat + 8, 0x0FFFFFFF);  // クラスタ 2 = 連鎖の終わり

        WriteDirEntry(image, bytesPerSector * 3, "FAT32VOL", "", 0x08, 0, 0, 0, 0x53C0, 0x58CF, 0, 0);
        return image;
    }

    // ===== REQ-188: 長いファイル名・サブディレクトリ・クラスタの連鎖 =====

    private static byte LfnChecksum(byte[] shortName)
    {
        byte sum = 0;
        foreach (var c in shortName) sum = (byte)(((sum & 1) << 7) + (sum >> 1) + c);
        return sum;
    }

    /// <summary>長いファイル名のエントリを逆順に書き、続けて短いエントリを書く。書いたエントリ数を返す。</summary>
    private static int WriteNamedEntry(byte[] image, int offset, string longName, string shortBase, string shortExt,
        byte attributes, ushort cluster, uint size)
    {
        var sfn = System.Text.Encoding.ASCII.GetBytes(shortBase.PadRight(8) + shortExt.PadRight(3));
        var checksum = LfnChecksum(sfn);
        var chars = longName.Select(c => (ushort)c).Append((ushort)0).ToList();
        var count = (longName.Length + 12) / 13;
        while (chars.Count < count * 13) chars.Add(0xFFFF);
        int[] positions = [1, 3, 5, 7, 9, 14, 16, 18, 20, 22, 24, 28, 30];
        for (var n = count; n >= 1; n--)
        {
            var entry = offset;
            image[entry] = (byte)(n | (n == count ? 0x40 : 0));
            image[entry + 11] = 0x0F;
            image[entry + 13] = checksum;
            for (var k = 0; k < 13; k++) WriteUInt16LE(image, entry + positions[k], chars[(n - 1) * 13 + k]);
            offset += 32;
        }
        WriteDirEntry(image, offset, shortBase, shortExt, attributes, 0x5000, 0x5B39, 0x5B39, 0x53C0, 0x5B39, cluster, size);
        return count + 1;
    }

    /// <summary>
    /// 長いファイル名とサブディレクトリを持つ FAT12 のイメージ（64 セクタ = 32 KB、REQ-188）。
    /// ルート: ボリュームラベル・README.TXT（"hello fat\n"）・"Long File Name.txt"（LONGFI~1.TXT）・削除されたエントリ・"Sub Dir"（SUBDIR~1、クラスタ 4 → 5）。
    /// "Sub Dir" は '.'・'..' と F00.TXT〜F19.TXT の 22 エントリで、1 クラスタ（16 エントリ）に収まらず FAT の連鎖で 2 クラスタ目に続く。
    /// macOS でマウントでき、fsck_msdos が問題を報告しないことを確かめた。
    /// </summary>
    public static byte[] CreateFat12ImageWithLongNames()
    {
        const int bps = 512;
        var image = new byte[bps * 64];
        image[0] = 0xEB; image[1] = 0x3C; image[2] = 0x90;
        WriteAscii(image, 3, "MSDOS5.0", 8);
        WriteUInt16LE(image, 11, bps);
        image[13] = 1;
        WriteUInt16LE(image, 14, 1);
        image[16] = 2;
        WriteUInt16LE(image, 17, 16);
        WriteUInt16LE(image, 19, 64);
        image[21] = 0xF8;
        WriteUInt16LE(image, 22, 1);
        WriteUInt16LE(image, 24, 32);
        WriteUInt16LE(image, 26, 2);
        image[36] = 0x80;
        image[38] = 0x29;
        WriteUInt32LE(image, 39, 0xCAFEBABE);
        WriteAscii(image, 43, "TESTVOL", 11);
        WriteAscii(image, 54, "FAT12", 8);
        WriteUInt16LE(image, 510, 0xAA55);

        // FAT: 0 = 0xFF8、1 = 0xFFF、2・3 = 終わり、4 → 5、5 = 終わり
        ushort[] entries = [0xFF8, 0xFFF, 0xFFF, 0xFFF, 5, 0xFFF];
        for (var copy = 0; copy < 2; copy++)
        {
            var fat = bps * (1 + copy);
            for (var i = 0; i < entries.Length; i += 2)
            {
                var a = entries[i]; var b = entries[i + 1];
                image[fat + i / 2 * 3] = (byte)a;
                image[fat + i / 2 * 3 + 1] = (byte)((a >> 8) | ((b & 0xF) << 4));
                image[fat + i / 2 * 3 + 2] = (byte)(b >> 4);
            }
        }

        const int root = bps * 3;
        int ClusterOffset(int cluster) => bps * (4 + cluster - 2);
        var pos = root;
        WriteDirEntry(image, pos, "TESTVOL", "", 0x08, 0, 0, 0, 0x53C0, 0x5B39, 0, 0); pos += 32;
        WriteDirEntry(image, pos, "README", "TXT", 0x20, 0x5000, 0x5B39, 0x5B39, 0x53C0, 0x5B39, 2, 10); pos += 32;
        "hello fat\n"u8.CopyTo(image.AsSpan(ClusterOffset(2)));
        pos += 32 * WriteNamedEntry(image, pos, "Long File Name.txt", "LONGFI~1", "TXT", 0x20, 3, 9);
        "long name"u8.CopyTo(image.AsSpan(ClusterOffset(3)));
        WriteDirEntry(image, pos, "_ELETED", "TXT", 0x20, 0, 0, 0, 0, 0, 0, 0); image[pos] = 0xE5; pos += 32;
        pos += 32 * WriteNamedEntry(image, pos, "Sub Dir", "SUBDIR~1", "", 0x10, 4, 0);

        var sub = ClusterOffset(4);
        WriteDirEntry(image, sub, ".", "", 0x10, 0, 0x5B39, 0x5B39, 0x53C0, 0x5B39, 4, 0);
        WriteDirEntry(image, sub + 32, "..", "", 0x10, 0, 0x5B39, 0x5B39, 0x53C0, 0x5B39, 0, 0);
        for (var i = 0; i < 20; i++)
        {
            var slot = i + 2;
            var offset = slot < 16 ? sub + slot * 32 : ClusterOffset(5) + (slot - 16) * 32;
            WriteDirEntry(image, offset, $"F{i:D2}", "TXT", 0x20, 0x5000, 0x5B39, 0x5B39, 0x53C0, 0x5B39, 0, 0);
        }
        return image;
    }
}
