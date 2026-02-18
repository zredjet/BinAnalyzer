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
    /// 最小FAT32イメージ（ブートセクタのみ、512バイト）。
    /// root_entry_count=0, fat_size_16=0 によりFAT32として判定される。
    /// </summary>
    public static byte[] CreateMinimalFat32Image()
    {
        var image = new byte[512];

        // Jump instruction
        image[0] = 0xEB; image[1] = 0x58; image[2] = 0x90;
        // OEM name
        WriteAscii(image, 3, "MSDOS5.0", 8);
        // Common BPB
        WriteUInt16LE(image, 11, 512);   // bytes_per_sector
        image[13] = 8;                    // sectors_per_cluster
        WriteUInt16LE(image, 14, 32);    // reserved_sector_count
        image[16] = 2;                    // num_fats
        WriteUInt16LE(image, 17, 0);     // root_entry_count = 0 (FAT32)
        WriteUInt16LE(image, 19, 0);     // total_sectors_16 = 0
        image[21] = 0xF8;                // media_type (FIXED_DISK)
        WriteUInt16LE(image, 22, 0);     // fat_size_16 = 0 (FAT32)
        WriteUInt16LE(image, 24, 32);    // sectors_per_track
        WriteUInt16LE(image, 26, 64);    // num_heads
        WriteUInt32LE(image, 28, 0);     // hidden_sectors
        WriteUInt32LE(image, 32, 8192);  // total_sectors_32

        // FAT32 extended BPB (offset 36)
        WriteUInt32LE(image, 36, 32);             // fat_size_32
        WriteUInt16LE(image, 40, 0);              // ext_flags
        WriteUInt16LE(image, 42, 0);              // fs_version
        WriteUInt32LE(image, 44, 2);              // root_cluster
        WriteUInt16LE(image, 48, 1);              // fs_info_sector
        WriteUInt16LE(image, 50, 6);              // backup_boot_sector
        // reserved: 12 bytes of zeros (offset 52-63, already initialized)
        image[64] = 0x80;                         // drive_number
        image[65] = 0x00;                         // reserved1
        image[66] = 0x29;                         // boot_signature
        WriteUInt32LE(image, 67, 0x87654321);     // volume_id
        WriteAscii(image, 71, "FAT32TEST", 11);  // volume_label
        WriteAscii(image, 82, "FAT32", 8);        // fs_type_string
        // boot_code (420 bytes): zeros (already initialized)
        // Boot signature word
        image[510] = 0x55;
        image[511] = 0xAA;

        return image;
    }
}
