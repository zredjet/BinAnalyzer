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

    [Fact]
    public void FatFormat_LoadsWithoutErrors()
    {
        var result = FormatValidator.Validate(new YamlFormatLoader().Load(FatFormatPath));
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void FatFormat_SmallImage_IsFat12ByClusterCount()
    {
        // CreateMinimalFat16Image は FAT16 の見出しの文字列を持つが、クラスタ数が 16 なので仕様では FAT12
        var decoded = Decode(FatTestDataGenerator.CreateMinimalFat16Image());

        var boot = decoded.Child("boot_sector");
        boot.Child("bytes_per_sector").Int().Should().Be(512);
        boot.Child("root_entry_count").Int().Should().Be(16);
        boot.Child("extended_bpb").Child("boot_signature_word").Validation!.Passed.Should().BeTrue();
        decoded.Child("cluster_count").Int().Should().Be(16);
        decoded.Child("fat_type").Int().Should().Be(12);
        decoded.Child("fat_table").Child("fat12_pairs").Elements().Should().NotBeEmpty();
    }

    [Fact]
    public void FatFormat_RootDirectory_DecodesEntries()
    {
        var entries = RootEntries(Decode(FatTestDataGenerator.CreateMinimalFat16Image()));

        var shorts = entries.Where(e => e.Child("is_lfn").Int() == 0 && e.Child("first_byte").Int() != 0).Select(e => e.Child("short")).ToList();
        shorts.Select(s => s.Child("short_name").Str()).Should().Equal("TEST", "HELLO.TXT", "SUBDIR");
        var flags = (DecodedFlags)shorts[1].Child("attributes");
        flags.FlagStates.Where(f => f.IsSet).Select(f => f.Name).Should().Equal("ARCHIVE");
        shorts[1].Child("file_size").Int().Should().Be(13);
        shorts[1].Child("modified").Str().Should().Be("2024-06-15 10:30:00");
        shorts[2].Child("is_subdirectory").Int().Should().Be(1);
        entries.Last().Child("first_byte").Label().Should().Be("end_of_directory");
    }

    [Fact]
    public void FatFormat_LongFileNames_AreAssembledFromLfnEntries()
    {
        var entries = RootEntries(Decode(FatTestDataGenerator.CreateFat12ImageWithLongNames()));

        var named = entries.Where(e => e.Child("is_lfn").Int() == 0).Select(e => e.Child("short")).ToList();
        var longFile = named.Single(s => s.Child("short_name").Str() == "LONGFI~1.TXT");
        longFile.Child("long_name").Str().Should().Be("Long File Name.txt");
        ((DecodedBytes)longFile.Child("content_preview")).RawBytes.ToArray().Should().Equal("long name"u8.ToArray());
        named.Single(s => s.Child("short_name").Str() == "SUBDIR~1").Child("long_name").Str().Should().Be("Sub Dir");
        // 削除されたエントリは長い名前を付けない
        var deleted = entries.Single(e => e.Child("first_byte").Label() == "deleted");
        ((DecodedStruct)deleted.Child("short")).Children.Should().NotContain(c => c.Name == "long_name");
        var lfn = entries.First(e => e.Child("is_lfn").Int() == 1).Child("lfn");
        lfn.Child("sequence").Bits("last_entry").Should().Be(1);
        lfn.Child("name_part").Str().Should().Be("e.txt");
    }

    [Fact]
    public void FatFormat_Subdirectory_FollowsClusterChain()
    {
        var decoded = Decode(FatTestDataGenerator.CreateFat12ImageWithLongNames());
        var subDir = RootEntries(decoded).Where(e => e.Child("is_lfn").Int() == 0).Select(e => e.Child("short"))
            .Single(s => s.Child("short_name").Str() == "SUBDIR~1");

        var chain = subDir.Child("subdirectory");
        chain.Child("cluster").Int().Should().Be(4);
        chain.Child("next_in_chain").Int().Should().Be(5);
        chain.Child("chain_continues").Int().Should().Be(1);
        var next = chain.Child("next_cluster");
        next.Child("cluster").Int().Should().Be(5);
        next.Child("chain_continues").Int().Should().Be(0);
        var names = chain.FindAll("short_name").Select(n => n.Str()).ToList();
        names.Should().StartWith([".", ".."]);
        names.Where(n => n.StartsWith('F')).Should().HaveCount(20);
        names.Should().Contain("F19.TXT");
    }

    [Fact]
    public void FatFormat_Fat32_ReadsFsInfoAndRootCluster()
    {
        var decoded = Decode(FatTestDataGenerator.CreateMinimalFat32Image());

        decoded.Child("fat_type").Int().Should().Be(32);
        var ext = decoded.Child("boot_sector").Child("extended_bpb");
        ext.Child("root_cluster").Int().Should().Be(2);
        var fsinfo = decoded.Child("fsinfo");
        fsinfo.Child("lead_signature").Validation!.Passed.Should().BeTrue();
        fsinfo.Child("free_count").Int().Should().Be(4);
        var root = decoded.Child("root_directory_chain");
        root.Child("cluster").Int().Should().Be(2);
        root.Find("short_name").Str().Should().Be("FAT32VOL");
        decoded.Child("fat_table").Child("fat32_entries").Elements()[2].Int().Should().Be(0x0FFFFFFF);
    }

    [Fact]
    public void FatFormat_TreeOutput_HidesWorkingValues()
    {
        var output = new TreeOutputFormatter().Format(Decode(FatTestDataGenerator.CreateFat12ImageWithLongNames()));

        output.Should().Contain("Long File Name.txt");
        output.Should().NotContain("lfn_acc");
        output.Should().NotContain("attr_peek");
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(FatFormatPath));

    private static IReadOnlyList<DecodedNode> RootEntries(DecodedStruct root) =>
        root.Child("root_directory").Child("entries").Elements();
}
