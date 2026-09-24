using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class ZipParsingTests
{
    private static readonly string ZipFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "zip.bdef.yaml");

    [Fact]
    public void ZipFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(ZipFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ZipFormat_DecodesMinimalZip()
    {
        var decoded = Decode(ZipTestDataGenerator.CreateMinimalZip());

        decoded.Name.Should().Be("ZIP");
        Value(decoded, "entry_count").Should().Be(1L);
        Value(decoded, "is_zip64").Should().Be(0L);
        ((DecodedArray)Child(Child(decoded, "central_directory"), "entries")).Elements.Should().HaveCount(1);
        ((DecodedArray)Child(decoded, "local_files")).Elements.Should().HaveCount(1);
    }

    [Fact]
    public void ZipFormat_LocalFileHeader_DecodesCorrectly()
    {
        var decoded = Decode(ZipTestDataGenerator.CreateMinimalZip());

        var localFile = ((DecodedArray)Child(decoded, "local_files")).Elements[0];
        var header = Child(localFile, "header");
        var signature = (DecodedInteger)Child(header, "signature");
        signature.Value.Should().Be(0x04034B50);
        signature.Validation!.Passed.Should().BeTrue();

        var compression = (DecodedInteger)Child(header, "compression");
        compression.Value.Should().Be(0);
        compression.EnumLabel.Should().Be("stored");
        ((DecodedString)Child(header, "filename")).Value.Should().Be("hello.txt");

        // 無圧縮のデータは Central Directory の compressed_size_actual バイト
        var data = (DecodedBytes)Child(Child(localFile, "data"), "data");
        data.RawBytes.ToArray().Should().Equal("Hello"u8.ToArray());
    }

    [Fact]
    public void ZipFormat_CentralDirectory_DecodesCorrectly()
    {
        var decoded = Decode(ZipTestDataGenerator.CreateMinimalZip());

        var entry = ((DecodedArray)Child(Child(decoded, "central_directory"), "entries")).Elements[0];
        ((DecodedInteger)Child(entry, "signature")).Validation!.Passed.Should().BeTrue();
        ((DecodedString)Child(entry, "filename")).Value.Should().Be("hello.txt");
        Value(entry, "compressed_size_actual").Should().Be(5L);
        Value(entry, "local_header_offset_actual").Should().Be(0L);
    }

    [Fact]
    public void ZipFormat_EndOfCentralDir_DecodesCorrectly()
    {
        var decoded = Decode(ZipTestDataGenerator.CreateMinimalZip());

        var eocd = Child(decoded, "end_of_central_directory");
        ((DecodedInteger)Child(eocd, "signature")).Validation!.Passed.Should().BeTrue();
        ((DecodedInteger)Child(eocd, "total_entries")).Value.Should().Be(1);
    }

    [Fact]
    public void ZipFormat_TreeOutput_ContainsExpectedElements()
    {
        var output = new TreeOutputFormatter().Format(Decode(ZipTestDataGenerator.CreateMinimalZip()));

        output.Should().Contain("ZIP");
        output.Should().Contain("central_directory");
        output.Should().Contain("local_files");
        output.Should().Contain("stored");
        output.Should().Contain("hello.txt");
    }

    [Fact]
    public void StreamedZip_UsesCentralDirectorySizesAndReadsDataDescriptors()
    {
        // Local File Header のサイズが 0 のストリーム出力（.NET ZipArchive）。以前は 2 つ目以降のエントリが崩れていた
        var decoded = Decode(ZipTestDataGenerator.CreateStreamedDeflateZip());

        var files = ((DecodedArray)Child(decoded, "local_files")).Elements;
        files.Should().HaveCount(2);
        var expected = new[] { (Name: "dd.txt", Size: 220), (Name: "second.txt", Size: 6) };
        for (var i = 0; i < files.Count; i++)
        {
            var header = Child(files[i], "header");
            ((DecodedString)Child(header, "filename")).Value.Should().Be(expected[i].Name);
            ((DecodedInteger)Child(header, "compressed_size")).Value.Should().Be(0);

            var content = (DecodedCompressed)Child(Child(files[i], "data"), "content");
            content.Algorithm.Should().Be("deflate");
            content.DecompressedSize.Should().Be(expected[i].Size);

            var descriptor = Child(files[i], "data_descriptor");
            ((DecodedInteger)Child(descriptor, "first_word")).Value.Should().Be(0x08074B50);
            ((DecodedInteger)Child(descriptor, "uncompressed_size")).Value.Should().Be(expected[i].Size);
        }
    }

    [Fact]
    public void Zip64_ReadsLocatorExtraFieldAndUnsignedDataDescriptor()
    {
        var decoded = Decode(ZipTestDataGenerator.CreateZip64WithUnsignedDataDescriptor());

        Value(decoded, "is_zip64").Should().Be(1L);
        Value(decoded, "entry_count").Should().Be(1L);
        // コメント付きなので End of Central Directory は先頭から署名を探して見つける
        ((DecodedString)Child(Child(decoded, "end_of_central_directory"), "comment")).Value.Should().Be("comment");
        ((DecodedInteger)Child(Child(decoded, "zip64_end_of_central_directory"), "signature")).Validation!.Passed.Should().BeTrue();

        var entry = ((DecodedArray)Child(Child(decoded, "central_directory"), "entries")).Elements[0];
        ((DecodedInteger)Child(entry, "compressed_size")).Value.Should().Be(0xFFFFFFFF);
        Value(entry, "compressed_size_actual").Should().Be(6L);
        Value(entry, "uses_zip64_sizes").Should().Be(1L);

        var file = ((DecodedArray)Child(decoded, "local_files")).Elements[0];
        ((DecodedBytes)Child(Child(file, "data"), "data")).RawBytes.ToArray().Should().Equal("zip64!"u8.ToArray());
        var descriptor = (DecodedStruct)Child(file, "data_descriptor");
        // 署名なし: 先頭の 4 バイトが CRC-32
        ((DecodedInteger)Child(descriptor, "first_word")).Value.Should().Be(((DecodedInteger)Child(entry, "crc32")).Value);
        descriptor.Children.Should().NotContain(c => c.Name == "crc32");
        ((DecodedInteger)Child(descriptor, "compressed_size_64")).Value.Should().Be(6);
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(ZipFormatPath));

    private static DecodedNode Child(DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);

    private static object Value(DecodedNode node, string name) => ((DecodedVirtual)Child(node, name)).Value;
}
