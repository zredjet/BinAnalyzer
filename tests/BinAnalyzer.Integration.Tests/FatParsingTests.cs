using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class FatParsingTests
{
    private static readonly string FatFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "fat.bdef.yaml");

    private static DecodedNode Navigate(DecodedStruct root, params string[] path)
    {
        DecodedNode current = root;
        foreach (var name in path)
        {
            var s = (DecodedStruct)current;
            current = s.Children.First(c => c.Name == name);
        }
        return current;
    }

    [Fact]
    public void FatFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(FatFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue(
            because: string.Join("; ", result.Errors.Select(e => e.Message)));
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void FatFormat_Fat16_BpbFields_DecodeCorrectly()
    {
        var data = FatTestDataGenerator.CreateMinimalFat16Image();
        var format = new YamlFormatLoader().Load(FatFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var bootSector = (DecodedStruct)Navigate(decoded, "boot_sector");

        Navigate(bootSector, "oem_name")
            .Should().BeOfType<DecodedString>().Which.Value.Should().Be("MSDOS5.0");
        Navigate(bootSector, "bytes_per_sector")
            .Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(512);
        Navigate(bootSector, "sectors_per_cluster")
            .Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(1);
        Navigate(bootSector, "reserved_sector_count")
            .Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(1);
        Navigate(bootSector, "num_fats")
            .Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(2);
        Navigate(bootSector, "root_entry_count")
            .Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(16);
        Navigate(bootSector, "media_type")
            .Should().BeOfType<DecodedInteger>().Which.EnumLabel.Should().Be("FIXED_DISK");
        Navigate(bootSector, "fat_size_16")
            .Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(1);
        Navigate(bootSector, "total_sectors")
            .Should().BeOfType<DecodedVirtual>().Which.Value.Should().Be(20L);
    }

    [Fact]
    public void FatFormat_Fat16_RootDirectory_DecodesCorrectly()
    {
        var data = FatTestDataGenerator.CreateMinimalFat16Image();
        var format = new YamlFormatLoader().Load(FatFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var rootDir = (DecodedStruct)Navigate(decoded, "root_directory");
        var entriesArray = rootDir.Children.First(c => c.Name == "entries")
            .Should().BeOfType<DecodedArray>().Subject;

        // 16 entries total (3 real + 13 empty, all parsed)
        entriesArray.Elements.Should().HaveCount(16);

        // Volume label entry
        var volumeEntry = (DecodedStruct)entriesArray.Elements[0];
        Navigate(volumeEntry, "filename")
            .Should().BeOfType<DecodedString>().Which.Value.Should().Be("TEST    ");

        // HELLO.TXT entry
        var helloEntry = (DecodedStruct)entriesArray.Elements[1];
        Navigate(helloEntry, "filename")
            .Should().BeOfType<DecodedString>().Which.Value.Should().Be("HELLO   ");
        Navigate(helloEntry, "extension")
            .Should().BeOfType<DecodedString>().Which.Value.Should().Be("TXT");
        Navigate(helloEntry, "file_size")
            .Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(13);

        // SUBDIR entry
        var subdirEntry = (DecodedStruct)entriesArray.Elements[2];
        Navigate(subdirEntry, "filename")
            .Should().BeOfType<DecodedString>().Which.Value.Should().Be("SUBDIR  ");
    }

    [Fact]
    public void FatFormat_Fat16_DirectoryAttributes_DecodeCorrectly()
    {
        var data = FatTestDataGenerator.CreateMinimalFat16Image();
        var format = new YamlFormatLoader().Load(FatFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var rootDir = (DecodedStruct)Navigate(decoded, "root_directory");
        var entriesArray = rootDir.Children.First(c => c.Name == "entries")
            .Should().BeOfType<DecodedArray>().Subject;

        // Volume label: attributes = 0x08 (VOLUME_LABEL bit)
        Navigate((DecodedStruct)entriesArray.Elements[0], "attributes")
            .Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x08);

        // HELLO.TXT: attributes = 0x20 (ARCHIVE bit)
        Navigate((DecodedStruct)entriesArray.Elements[1], "attributes")
            .Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x20);

        // SUBDIR: attributes = 0x10 (DIRECTORY bit)
        Navigate((DecodedStruct)entriesArray.Elements[2], "attributes")
            .Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x10);
    }

    [Fact]
    public void FatFormat_Fat16_Timestamps_DecodeCorrectly()
    {
        var data = FatTestDataGenerator.CreateMinimalFat16Image();
        var format = new YamlFormatLoader().Load(FatFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var rootDir = (DecodedStruct)Navigate(decoded, "root_directory");
        var entriesArray = rootDir.Children.First(c => c.Name == "entries")
            .Should().BeOfType<DecodedArray>().Subject;

        // Check timestamps on HELLO.TXT entry
        // DOSタイムスタンプ: 2024-06-15 10:30:00
        //   modification_date = 0x58CF, modification_time = 0x53C0
        var helloEntry = (DecodedStruct)entriesArray.Elements[1];

        Navigate(helloEntry, "mod_year")
            .Should().BeOfType<DecodedVirtual>().Which.Value.Should().Be(2024L);
        Navigate(helloEntry, "mod_month")
            .Should().BeOfType<DecodedVirtual>().Which.Value.Should().Be(6L);
        Navigate(helloEntry, "mod_day")
            .Should().BeOfType<DecodedVirtual>().Which.Value.Should().Be(15L);
        Navigate(helloEntry, "mod_hour")
            .Should().BeOfType<DecodedVirtual>().Which.Value.Should().Be(10L);
        Navigate(helloEntry, "mod_minute")
            .Should().BeOfType<DecodedVirtual>().Which.Value.Should().Be(30L);
        Navigate(helloEntry, "mod_second")
            .Should().BeOfType<DecodedVirtual>().Which.Value.Should().Be(0L);
    }

    [Fact]
    public void FatFormat_Fat32_ExtendedBpb_DecodesCorrectly()
    {
        var data = FatTestDataGenerator.CreateMinimalFat32Image();
        var format = new YamlFormatLoader().Load(FatFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var bootSector = (DecodedStruct)Navigate(decoded, "boot_sector");

        // FAT32 indicator: root_entry_count = 0
        Navigate(bootSector, "root_entry_count")
            .Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0);
        Navigate(bootSector, "fat_type_hint")
            .Should().BeOfType<DecodedVirtual>().Which.Value.Should().Be(1L);

        // Navigate into switch result
        var extBpb = (DecodedStruct)Navigate(bootSector, "extended_bpb");

        Navigate(extBpb, "fat_size_32")
            .Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(32);
        Navigate(extBpb, "root_cluster")
            .Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(2);
        Navigate(extBpb, "fs_info_sector")
            .Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(1);
        Navigate(extBpb, "backup_boot_sector")
            .Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(6);
        Navigate(extBpb, "volume_label")
            .Should().BeOfType<DecodedString>().Which.Value.Should().Be("FAT32TEST  ");
        Navigate(extBpb, "fs_type_string")
            .Should().BeOfType<DecodedString>().Which.Value.Should().Be("FAT32   ");
        Navigate(extBpb, "boot_signature_word")
            .Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0xAA55);

        // root_directory should not exist (skipped by if condition)
        decoded.Children.Should().NotContain(c => c.Name == "root_directory");
    }

    [Fact]
    public void FatFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = FatTestDataGenerator.CreateMinimalFat16Image();
        var format = new YamlFormatLoader().Load(FatFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("boot_sector");
        output.Should().Contain("oem_name");
        output.Should().Contain("bytes_per_sector");
        output.Should().Contain("FIXED_DISK");
        output.Should().Contain("root_directory");
        output.Should().Contain("HELLO");
        output.Should().Contain("fat_type_hint");
    }
}
