using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

public class ChecksumParsingTests
{
    [Fact]
    public void Load_ChecksumField_ParsesCorrectly()
    {
        var yaml = """
            name: test
            endianness: big
            root: root
            structs:
              root:
                - name: type
                  type: ascii
                  size: "4"
                - name: data
                  type: bytes
                  size: "8"
                - name: crc
                  type: uint32
                  checksum:
                    algorithm: crc32
                    fields: [type, data]
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var crcField = format.Structs["root"].Fields[2];
        crcField.Checksum.Should().NotBeNull();
        crcField.Checksum!.Algorithm.Should().Be("crc32");
        crcField.Checksum.FieldNames.Should().BeEquivalentTo(["type", "data"]);
    }

    [Theory]
    [InlineData("crc16-ccitt")]
    [InlineData("crc16-ibm")]
    [InlineData("adler32")]
    [InlineData("md5")]
    [InlineData("sha1")]
    [InlineData("sha256")]
    [InlineData("crc8")]
    [InlineData("crc8-maxim")]
    [InlineData("crc8-cdma2000")]
    [InlineData("crc64-ecma")]
    [InlineData("xxhash32")]
    [InlineData("xxhash64")]
    [InlineData("fletcher16")]
    [InlineData("fletcher32")]
    [InlineData("sha384")]
    [InlineData("sha512")]
    public void Load_NewAlgorithmNames_ParsesCorrectly(string algorithm)
    {
        var yaml = $"""
            name: test
            endianness: big
            root: root
            structs:
              root:
                - name: data
                  type: bytes
                  size: "8"
                - name: checksum
                  type: bytes
                  size: "16"
                  checksum:
                    algorithm: {algorithm}
                    fields: [data]
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var checksumField = format.Structs["root"].Fields[1];
        checksumField.Checksum.Should().NotBeNull();
        checksumField.Checksum!.Algorithm.Should().Be(algorithm);
        checksumField.Checksum.FieldNames.Should().BeEquivalentTo(["data"]);
    }

    [Fact]
    public void Load_FieldWithoutChecksum_HasNullChecksum()
    {
        var yaml = """
            name: test
            endianness: big
            root: root
            structs:
              root:
                - name: value
                  type: uint32
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["root"].Fields[0];
        field.Checksum.Should().BeNull();
    }

    [Fact]
    public void Load_ChecksumWithRange_ParsesCorrectly()
    {
        var yaml = """
            name: test
            endianness: big
            root: root
            structs:
              root:
                - name: data
                  type: bytes
                  size: "16"
                - name: crc
                  type: uint32
                  checksum:
                    algorithm: crc32
                    range:
                      offset: "0"
                      size: "{data_len}"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var crcField = format.Structs["root"].Fields[1];
        crcField.Checksum.Should().NotBeNull();
        crcField.Checksum!.Algorithm.Should().Be("crc32");
        crcField.Checksum.Range.Should().NotBeNull();
        crcField.Checksum.Range!.OffsetExpression.OriginalText.Should().Be("0");
        crcField.Checksum.Range!.SizeExpression.OriginalText.Should().Be("{data_len}");
        crcField.Checksum.FieldNames.Should().BeEmpty();
    }

    [Fact]
    public void Load_ChecksumWithRanges_ParsesCorrectly()
    {
        var yaml = """
            name: test
            endianness: big
            root: root
            structs:
              root:
                - name: data
                  type: bytes
                  size: "16"
                - name: crc
                  type: uint32
                  checksum:
                    algorithm: crc32
                    ranges:
                      - offset: "0"
                        size: "4"
                      - offset: "8"
                        size: "8"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var crcField = format.Structs["root"].Fields[1];
        crcField.Checksum.Should().NotBeNull();
        crcField.Checksum!.Ranges.Should().HaveCount(2);
        crcField.Checksum.Ranges![0].OffsetExpression.OriginalText.Should().Be("0");
        crcField.Checksum.Ranges[0].SizeExpression.OriginalText.Should().Be("4");
        crcField.Checksum.Ranges[1].OffsetExpression.OriginalText.Should().Be("8");
        crcField.Checksum.Ranges[1].SizeExpression.OriginalText.Should().Be("8");
    }

    [Fact]
    public void Load_ChecksumWithExcludeSelf_ParsesCorrectly()
    {
        var yaml = """
            name: test
            endianness: big
            root: root
            structs:
              root:
                - name: crc
                  type: uint32
                  checksum:
                    algorithm: crc32
                    range:
                      offset: "0"
                      size: "16"
                    exclude_self: true
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var crcField = format.Structs["root"].Fields[0];
        crcField.Checksum.Should().NotBeNull();
        crcField.Checksum!.ExcludeSelf.Should().BeTrue();
    }

    [Fact]
    public void Load_ChecksumWithExcludeSelfDefault_IsFalse()
    {
        var yaml = """
            name: test
            endianness: big
            root: root
            structs:
              root:
                - name: crc
                  type: uint32
                  checksum:
                    algorithm: crc32
                    range:
                      offset: "0"
                      size: "16"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var crcField = format.Structs["root"].Fields[0];
        crcField.Checksum.Should().NotBeNull();
        crcField.Checksum!.ExcludeSelf.Should().BeFalse();
    }
}
