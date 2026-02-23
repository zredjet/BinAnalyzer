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

        format.Structs["strtab_section"].StringTableEncoding.Should().Be(StringTableEncoding.Ascii);
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
    public void DefaultStringTableIsNull()
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

        format.Structs["main"].StringTableEncoding.Should().BeNull();
    }

    [Fact]
    public void ParsesStringTableWithUtf8Encoding()
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
                string_table:
                  encoding: utf8
                fields:
                  - name: data
                    type: bytes
                    size: remaining
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        format.Structs["strtab_section"].StringTableEncoding.Should().Be(StringTableEncoding.Utf8);
    }

    [Fact]
    public void ParsesStringTableWithUtf16LeEncoding()
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
                string_table:
                  encoding: utf16le
                fields:
                  - name: data
                    type: bytes
                    size: remaining
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        format.Structs["strtab_section"].StringTableEncoding.Should().Be(StringTableEncoding.Utf16Le);
    }

    [Fact]
    public void ParsesStringTableWithUtf16BeEncoding()
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
                string_table:
                  encoding: utf16be
                fields:
                  - name: data
                    type: bytes
                    size: remaining
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        format.Structs["strtab_section"].StringTableEncoding.Should().Be(StringTableEncoding.Utf16Be);
    }

    [Fact]
    public void ParsesStringTableWithEncodingOmittedDefaultsToAscii()
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
                string_table:
                  encoding: ascii
                fields:
                  - name: data
                    type: bytes
                    size: remaining
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        format.Structs["strtab_section"].StringTableEncoding.Should().Be(StringTableEncoding.Ascii);
    }

    [Fact]
    public void ThrowsForInvalidEncoding()
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
                string_table:
                  encoding: euc-jp
                fields:
                  - name: data
                    type: bytes
                    size: remaining
            """;

        var loader = new YamlFormatLoader();
        var act = () => loader.LoadFromString(yaml);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown string_table encoding*euc-jp*");
    }

    [Fact]
    public void StringTableFalseIsNull()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                string_table: false
                fields:
                  - name: value
                    type: uint8
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        format.Structs["main"].StringTableEncoding.Should().BeNull();
    }
}
