using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

public class BitfieldParsingTests
{
    private const string BitfieldYaml = """
        name: test
        endianness: big
        root: root

        enums:
          mode_type:
            - value: 0
              label: off
            - value: 1
              label: normal

        structs:
          root:
            - name: flags
              type: bitfield
              size: "1"
              fields:
                - name: reserved
                  bits: "7:4"
                - name: flag_a
                  bits: "3"
                - name: mode
                  bits: "1:0"
                  enum: mode_type
        """;

    [Fact]
    public void Load_BitfieldType_ParsesCorrectly()
    {
        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(BitfieldYaml);

        var rootStruct = format.Structs["root"];
        rootStruct.Fields.Should().HaveCount(1);

        var field = rootStruct.Fields[0];
        field.Name.Should().Be("flags");
        field.Type.Should().Be(FieldType.Bitfield);
        field.Size.Should().Be(1);
    }

    [Fact]
    public void Load_BitfieldEntries_ParsedCorrectly()
    {
        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(BitfieldYaml);

        var field = format.Structs["root"].Fields[0];
        field.BitfieldEntries.Should().NotBeNull();
        field.BitfieldEntries.Should().HaveCount(3);
    }

    [Fact]
    public void Load_BitRange_ParsedCorrectly()
    {
        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(BitfieldYaml);

        var entries = format.Structs["root"].Fields[0].BitfieldEntries!;

        // "7:4" → high=7, low=4
        entries[0].Name.Should().Be("reserved");
        entries[0].BitHigh.Should().Be(7);
        entries[0].BitLow.Should().Be(4);

        // "3" → high=3, low=3
        entries[1].Name.Should().Be("flag_a");
        entries[1].BitHigh.Should().Be(3);
        entries[1].BitLow.Should().Be(3);

        // "1:0" → high=1, low=0
        entries[2].Name.Should().Be("mode");
        entries[2].BitHigh.Should().Be(1);
        entries[2].BitLow.Should().Be(0);
    }

    [Fact]
    public void Load_BitfieldEntryWithEnum_ParsedCorrectly()
    {
        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(BitfieldYaml);

        var entries = format.Structs["root"].Fields[0].BitfieldEntries!;

        entries[0].EnumRef.Should().BeNull();
        entries[2].EnumRef.Should().Be("mode_type");
    }
}
