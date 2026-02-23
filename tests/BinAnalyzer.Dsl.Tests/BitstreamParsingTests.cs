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
            .WithMessage("*unsupported field*data*");
    }

    [Fact]
    public void Load_BitstreamWithVirtual_Succeeds()
    {
        var yaml = """
            name: test
            endianness: big
            root: root
            structs:
              root:
                mode: bitstream
                fields:
                  - name: a
                    type: uint8
                    size: "4"
                  - name: computed
                    type: virtual
                    value: "{a}"
                  - name: b
                    type: uint8
                    size: "4"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var fields = format.Structs["root"].Fields;
        fields.Should().HaveCount(3);
        fields[1].Type.Should().Be(FieldType.Virtual);
    }

    [Fact]
    public void Load_BitstreamWithSwitch_Succeeds()
    {
        var yaml = """
            name: test
            endianness: big
            root: root
            structs:
              root:
                mode: bitstream
                fields:
                  - name: tag
                    type: uint8
                    size: "2"
                  - name: body
                    type: switch
                    switch_on: "{tag}"
                    cases:
                      "0": case0
                      "1": case1
                    default: case0
              case0:
                mode: bitstream
                fields:
                  - name: val
                    type: uint8
                    size: "6"
              case1:
                mode: bitstream
                fields:
                  - name: val
                    type: uint8
                    size: "6"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var fields = format.Structs["root"].Fields;
        fields.Should().HaveCount(2);
        fields[1].Type.Should().Be(FieldType.Switch);
    }

    [Fact]
    public void Load_BitstreamWithStruct_Succeeds()
    {
        var yaml = """
            name: test
            endianness: big
            root: root
            structs:
              root:
                mode: bitstream
                fields:
                  - name: header
                    type: uint8
                    size: "4"
                  - name: nested
                    type: struct
                    struct: inner
              inner:
                mode: bitstream
                fields:
                  - name: val
                    type: uint8
                    size: "4"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var fields = format.Structs["root"].Fields;
        fields.Should().HaveCount(2);
        fields[1].Type.Should().Be(FieldType.Struct);
    }

    // --- REQ-153: bit_order ---

    [Fact]
    public void ParsesBitOrderLsb()
    {
        var yaml = """
            name: test
            endianness: big
            root: root
            structs:
              root:
                mode: bitstream
                bit_order: lsb
                fields:
                  - name: value
                    type: uint8
                    size: "8"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var structDef = format.Structs["root"];
        structDef.IsBitstream.Should().BeTrue();
        structDef.BitOrder.Should().Be(BitOrder.Lsb);
    }

    [Fact]
    public void ParsesBitOrderMsb()
    {
        var yaml = """
            name: test
            endianness: big
            root: root
            structs:
              root:
                mode: bitstream
                bit_order: msb
                fields:
                  - name: value
                    type: uint8
                    size: "8"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var structDef = format.Structs["root"];
        structDef.IsBitstream.Should().BeTrue();
        structDef.BitOrder.Should().Be(BitOrder.Msb);
    }

    [Fact]
    public void BitOrderDefaultIsNull()
    {
        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(BitstreamYaml);

        var structDef = format.Structs["bitfields"];
        structDef.IsBitstream.Should().BeTrue();
        structDef.BitOrder.Should().BeNull();
    }

    [Fact]
    public void ThrowsForInvalidBitOrder()
    {
        var yaml = """
            name: test
            endianness: big
            root: root
            structs:
              root:
                mode: bitstream
                bit_order: middle
                fields:
                  - name: value
                    type: uint8
                    size: "8"
            """;

        var loader = new YamlFormatLoader();
        var act = () => loader.LoadFromString(yaml);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown bit_order*middle*");
    }

    [Fact]
    public void ThrowsForBitOrderOnNonBitstream()
    {
        var yaml = """
            name: test
            endianness: big
            root: root
            structs:
              root:
                bit_order: lsb
                fields:
                  - name: value
                    type: uint8
            """;

        var loader = new YamlFormatLoader();
        var act = () => loader.LoadFromString(yaml);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*bit_order*not*bitstream*");
    }
}
