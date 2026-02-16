using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

public class BitstreamParsingTests
{
    private const string BitstreamYaml = """
        name: test
        endianness: big
        root: root

        structs:
          root:
            - name: data
              type: struct
              struct: bitfields

          bitfields:
            mode: bitstream
            fields:
              - name: sample_rate
                type: uint32
                size: "20"
              - name: channels
                type: uint8
                size: "3"
              - name: bps
                type: uint8
                size: "5"
        """;

    [Fact]
    public void Load_BitstreamMode_SetsIsBitstreamTrue()
    {
        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(BitstreamYaml);

        var bitstreamStruct = format.Structs["bitfields"];
        bitstreamStruct.IsBitstream.Should().BeTrue();
    }

    [Fact]
    public void Load_StructWithoutMode_IsBitstreamIsFalse()
    {
        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(BitstreamYaml);

        var rootStruct = format.Structs["root"];
        rootStruct.IsBitstream.Should().BeFalse();
    }

    [Fact]
    public void Load_BitstreamFields_ParsedCorrectly()
    {
        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(BitstreamYaml);

        var fields = format.Structs["bitfields"].Fields;
        fields.Should().HaveCount(3);

        fields[0].Name.Should().Be("sample_rate");
        fields[0].Type.Should().Be(FieldType.UInt32);
        fields[0].Size.Should().Be(20);

        fields[1].Name.Should().Be("channels");
        fields[1].Type.Should().Be(FieldType.UInt8);
        fields[1].Size.Should().Be(3);

        fields[2].Name.Should().Be("bps");
        fields[2].Type.Should().Be(FieldType.UInt8);
        fields[2].Size.Should().Be(5);
    }

    [Fact]
    public void Load_InvalidMode_ThrowsException()
    {
        var yaml = """
            name: test
            endianness: big
            root: root
            structs:
              root:
                mode: invalid_mode
                fields:
                  - name: value
                    type: uint8
                    size: "8"
            """;

        var loader = new YamlFormatLoader();
        var act = () => loader.LoadFromString(yaml);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown struct mode*invalid_mode*");
    }

    [Fact]
    public void Load_BitstreamWithNonIntegerField_ThrowsException()
    {
        var yaml = """
            name: test
            endianness: big
            root: root
            structs:
              root:
                mode: bitstream
                fields:
                  - name: data
                    type: bytes
                    size: "8"
            """;

        var loader = new YamlFormatLoader();
        var act = () => loader.LoadFromString(yaml);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*non-integer field*data*");
    }
}
