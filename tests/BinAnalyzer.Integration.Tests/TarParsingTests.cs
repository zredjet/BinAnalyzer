using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class TarParsingTests
{
    private static readonly string TarFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "tar.bdef.yaml");

    [Fact]
    public void TarFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(TarFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void TarFormat_DecodesMinimalTar()
    {
        var data = TarTestDataGenerator.CreateMinimalTar();
        var format = new YamlFormatLoader().Load(TarFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("TAR");
        decoded.Children.Select(c => c.Name).Should().Equal("file_size", "entries", "end_of_archive");
    }

    [Fact]
    public void TarFormat_Header_DecodesCorrectly()
    {
        var data = TarTestDataGenerator.CreateMinimalTar();
        var format = new YamlFormatLoader().Load(TarFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var entries = decoded.Children.First(c => c.Name == "entries").Should().BeOfType<DecodedArray>().Subject;
        entries.Elements.Should().HaveCount(1);

        var header = entries.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        var name = header.Children[0].Should().BeOfType<DecodedString>().Subject;
        name.Name.Should().Be("name");
        name.Value.Should().Contain("hello.txt");

        var typeflag = header.Children[7].Should().BeOfType<DecodedInteger>().Subject;
        typeflag.Name.Should().Be("typeflag");
        typeflag.Value.Should().Be(0x30);
        typeflag.EnumLabel.Should().Be("regular_file");
    }

    [Fact]
    public void TarFormat_Magic_ValidatesCorrectly()
    {
        var data = TarTestDataGenerator.CreateMinimalTar();
        var format = new YamlFormatLoader().Load(TarFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var entries = decoded.Children.First(c => c.Name == "entries").Should().BeOfType<DecodedArray>().Subject;
        var header = entries.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        var magic = header.Children[9].Should().BeOfType<DecodedString>().Subject;
        magic.Name.Should().Be("magic");
        magic.Value.Should().Contain("ustar");
        // 値は "ustar\0"。NUL を含めて比べていたため、以前は常に ✗ だった
        magic.Validation!.Passed.Should().BeTrue();
    }

    [Fact]
    public void TarFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = TarTestDataGenerator.CreateMinimalTar();
        var format = new YamlFormatLoader().Load(TarFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("TAR");
        output.Should().Contain("entries");
        output.Should().Contain("hello.txt");
        output.Should().Contain("regular_file");
        output.Should().Contain("uname");
    }

    [Fact]
    public void TarFormat_VirtualFields_ComputeCorrectly()
    {
        var data = TarTestDataGenerator.CreateMinimalTar();
        var format = new YamlFormatLoader().Load(TarFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var entries = decoded.Children.First(c => c.Name == "entries").Should().BeOfType<DecodedArray>().Subject;
        entries.Elements.Should().HaveCount(1);

        var entry = entries.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        // file_size_bytes = parse_int("00000000000\0", 8) = 0
        var fileSizeBytes = entry.Children.First(c => c.Name == "file_size_bytes")
            .Should().BeOfType<DecodedVirtual>().Subject;
        fileSizeBytes.Value.Should().Be(0L);

        // data_blocks_size = ((0 + 511) / 512) * 512 = 0
        var dataBlocksSize = entry.Children.First(c => c.Name == "data_blocks_size")
            .Should().BeOfType<DecodedVirtual>().Subject;
        dataBlocksSize.Value.Should().Be(0L);

        // data field should be skipped (condition: file_size_bytes > 0 is false)
        entry.Children.Should().NotContain(c => c.Name == "data");
    }

    [Fact]
    public void PaxAndGnuTar_SplitsRecordsLongNamesAndEndOfArchive()
    {
        var data = TarTestDataGenerator.CreatePaxAndGnuTar();
        var decoded = new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(TarFormatPath));

        // 終端の 0 ブロックはエントリとして読まない
        var entries = ((DecodedArray)Child(decoded, "entries")).Elements;
        entries.Select(e => ((DecodedInteger)Child(e, "typeflag")).EnumLabel)
            .Should().Equal("pax_extended", "regular_file", "gnu_longname", "regular_file");
        entries.Select(e => Child(e, "magic").Validation!.Passed).Should().OnlyContain(passed => passed);

        var records = ((DecodedArray)Child(Child(entries[0], "data"), "records")).Elements;
        records.Select(r => ((DecodedString)Child(r, "keyword")).Value).Should().Equal("path", "mtime");
        ((DecodedString)Child(records[0], "value")).Value.Should().Be("dir/a-very-long-file-name-that-is-stored-in-a-pax-record.txt");
        ((DecodedString)Child(records[1], "value")).Value.Should().Be("1700000000.5");

        ((DecodedString)Child(Child(entries[2], "data"), "long_name")).Value
            .Should().Be("gnu/another-long-name-for-the-gnu-longlink-entry.txt\0");
        ((DecodedString)Child(entries[2], "magic")).Value.Should().Be("ustar ");

        var content = (DecodedBytes)Child(Child(entries[3], "data"), "content");
        content.RawBytes.ToArray().Should().Equal("gnu"u8.ToArray());
        ((DecodedVirtual)Child(entries[3], "is_last")).Value.Should().Be(1L);
        ((DecodedVirtual)Child(entries[3], "mtime_value")).Value.Should().Be(1_700_000_000L);

        // 4 エントリ × 1024 バイトの後ろが終端（0 ブロック 2 個 + 10240 バイトまでの詰め物）
        var end = (DecodedBytes)Child(decoded, "end_of_archive");
        end.Offset.Should().Be(4 * 1024);
        end.RawBytes.Length.Should().Be(data.Length - 4 * 1024);
    }

    private static DecodedNode Child(DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);
}
