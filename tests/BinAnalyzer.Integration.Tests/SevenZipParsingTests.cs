using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class SevenZipParsingTests
{
    private static readonly string SevenZipFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "7z.bdef.yaml");

    [Fact]
    public void SevenZipFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(SevenZipFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void SevenZipFormat_DecodesMinimal7z()
    {
        var data = SevenZipTestDataGenerator.CreateMinimal7z();
        var format = new YamlFormatLoader().Load(SevenZipFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("7z");
        decoded.Children.Select(c => c.Name).Should().Equal("signature_header", "packed_streams", "next_header");
        // 最小のヘッダは kHeader + kEnd
        var body = Child(Child(decoded, "next_header"), "body");
        var sections = ((DecodedArray)Child(body, "sections")).Elements;
        sections.Should().ContainSingle();
        ((DecodedInteger)Child(sections[0], "section_id")).EnumLabel.Should().Be("kEnd");
    }

    [Fact]
    public void SevenZipFormat_SignatureHeader_DecodesCorrectly()
    {
        var data = SevenZipTestDataGenerator.CreateMinimal7z();
        var format = new YamlFormatLoader().Load(SevenZipFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var sigHeader = decoded.Children[0].Should().BeOfType<DecodedStruct>().Subject;

        var signature = sigHeader.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        signature.Name.Should().Be("signature");
        signature.ValidationPassed.Should().BeTrue();

        var majorVersion = sigHeader.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        majorVersion.Name.Should().Be("major_version");
        majorVersion.Value.Should().Be(0);

        var minorVersion = sigHeader.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        minorVersion.Name.Should().Be("minor_version");
        minorVersion.Value.Should().Be(4);
    }

    [Fact]
    public void SevenZipFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = SevenZipTestDataGenerator.CreateMinimal7z();
        var format = new YamlFormatLoader().Load(SevenZipFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("7z");
        output.Should().Contain("signature_header");
        output.Should().Contain("signature");
        output.Should().Contain("next_header");
    }

    [Fact]
    public void PlainHeader_DecodesStreamsAndFilesInfo()
    {
        var decoded = Decode(SevenZipTestDataGenerator.Create7zWithCopyFolder(encodeHeader: false));

        var signatureHeader = Child(decoded, "signature_header");
        ((DecodedInteger)Child(signatureHeader, "start_header_crc")).ChecksumValid.Should().BeTrue();
        ((DecodedInteger)Child(signatureHeader, "next_header_crc")).ChecksumValid.Should().BeTrue();
        ((DecodedBytes)Child(decoded, "packed_streams")).RawBytes.Length.Should().Be(13);

        var nextHeader = Child(decoded, "next_header");
        ((DecodedInteger)Child(nextHeader, "header_type")).EnumLabel.Should().Be("kHeader");
        var sections = ((DecodedArray)Child(Child(nextHeader, "body"), "sections")).Elements;
        sections.Select(s => ((DecodedInteger)Child(s, "section_id")).EnumLabel)
            .Should().Equal("kMainStreamsInfo", "kFilesInfo", "kEnd");

        var items = ((DecodedArray)Child(Child(sections[0], "body"), "items")).Elements;
        items.Select(i => ((DecodedInteger)Child(i, "item_id")).EnumLabel)
            .Should().Equal("kPackInfo", "kUnPackInfo", "kSubStreamsInfo", "kEnd");

        var codersInfo = Child(items[1], "body");
        var folder = ((DecodedArray)Child(codersInfo, "folders")).Elements.Single();
        var coder = ((DecodedArray)Child(folder, "coders")).Elements.Single();
        ((DecodedVirtual)Child(Child(coder, "method"), "method_id")).EnumLabel.Should().Be("Copy");
        ((DecodedVirtual)Child(folder, "packed_stream_count")).Value.Should().Be(1L);

        // SubStreamsInfo: 2 ファイル、サイズは最初の 1 個だけ明示、CRC は 2 個
        var subProperties = ((DecodedArray)Child(Child(items[2], "body"), "properties")).Elements;
        var counts = Child(subProperties[0], "body");
        ((DecodedVirtual)Child(counts, "total_substreams")).Value.Should().Be(2L);
        ((DecodedVirtual)Child(counts, "sizes_to_read")).Value.Should().Be(1L);
        ((DecodedVirtual)Child(counts, "crcs_to_read")).Value.Should().Be(2L);
        var sizes = ((DecodedArray)Child(Child(subProperties[1], "body"), "sizes")).Elements;
        ((DecodedVirtual)Child(sizes.Single(), "number")).Value.Should().Be(6L);
        var crcs = (DecodedArray)Child(Child(Child(subProperties[2], "body"), "digests"), "crcs");
        crcs.Elements.Should().HaveCount(2);

        var fileProperties = ((DecodedArray)Child(Child(sections[1], "body"), "properties")).Elements;
        fileProperties.Select(p => ((DecodedInteger)Child(p, "file_property_id")).EnumLabel)
            .Should().Equal("kName", "kDummy", "kMTime", "kWinAttributes", "kEnd");
        ((DecodedString)Child(Child(fileProperties[0], "body"), "names")).Value.Should().Be("a.txt\0b.txt\0");
        // kDummy のサイズ 200 は 2 バイトの可変長整数（0x80 0xC8）
        var dummySize = Child(fileProperties[1], "property_size");
        ((DecodedVirtual)Child(dummySize, "extra_bytes")).Value.Should().Be(1L);
        ((DecodedVirtual)Child(dummySize, "number")).Value.Should().Be(200L);
        ((DecodedArray)Child(Child(fileProperties[2], "body"), "times")).Elements.Should().HaveCount(2);
    }

    [Fact]
    public void EncodedHeader_DecodesPackedHeaderLocationAndCoder()
    {
        var decoded = Decode(SevenZipTestDataGenerator.Create7zWithCopyFolder(encodeHeader: true));

        ((DecodedInteger)Child(Child(decoded, "signature_header"), "next_header_crc")).ChecksumValid.Should().BeTrue();
        var nextHeader = Child(decoded, "next_header");
        ((DecodedInteger)Child(nextHeader, "header_type")).EnumLabel.Should().Be("kEncodedHeader");

        var items = ((DecodedArray)Child(Child(nextHeader, "body"), "items")).Elements;
        items.Select(i => ((DecodedInteger)Child(i, "item_id")).EnumLabel).Should().Equal("kPackInfo", "kUnPackInfo", "kEnd");
        // 圧縮ヘッダはファイルの内容（13 バイト）の後ろ
        ((DecodedVirtual)Child(Child(Child(items[0], "body"), "pack_pos"), "number")).Value.Should().Be(13L);
        var codersInfo = Child(items[1], "body");
        ((DecodedVirtual)Child(codersInfo, "folders_with_crc")).Value.Should().Be(1L);
        ((DecodedInteger)Child(codersInfo, "unpack_size_marker")).Validation!.Passed.Should().BeTrue();
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(SevenZipFormatPath));

    private static DecodedNode Child(DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);
}
