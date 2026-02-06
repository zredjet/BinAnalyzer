using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

public class StringTableParsingTests
{
    [Fact]
    public void ParsesStructWithStringTable()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: strtab
                  type: struct
                  struct: strtab_section
              strtab_section:
                string_table: true
                fields:
                  - name: data
                    type: bytes
                    size: remaining
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        format.Structs["strtab_section"].IsStringTable.Should().BeTrue();
    }

    [Fact]
    public void ParsesFieldWithStringTableRef()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: name_offset
                  type: uint32
                  string_table: strtab
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        format.Structs["main"].Fields[0].StringTableRef.Should().Be("strtab");
    }

    [Fact]
    public void DefaultStringTableIsFalse()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                endianness: little
                fields:
                  - name: value
                    type: uint16
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        format.Structs["main"].IsStringTable.Should().BeFalse();
    }
}
