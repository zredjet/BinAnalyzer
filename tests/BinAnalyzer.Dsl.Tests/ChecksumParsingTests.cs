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
}
