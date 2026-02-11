using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

public class EndiannessParsingTests
{
    [Fact]
    public void ParsesOldFormat_FieldListDirectly()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: value
                  type: uint16
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        format.Structs["main"].Fields.Should().HaveCount(1);
        format.Structs["main"].Fields[0].Name.Should().Be("value");
        format.Structs["main"].Endianness.Should().BeNull();
    }

    [Fact]
    public void ParsesNewFormat_WithEndianness()
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

        format.Structs["main"].Endianness.Should().Be(Endianness.Little);
        format.Structs["main"].Fields.Should().HaveCount(1);
        format.Structs["main"].Fields[0].Name.Should().Be("value");
    }

    [Fact]
    public void ParsesNewFormat_WithAlign()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                align: 8
                fields:
                  - name: value
                    type: uint32
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        format.Structs["main"].Align.Should().Be(8);
        format.Structs["main"].Fields.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesFieldLevelEndianness()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: be_value
                  type: uint16
                - name: le_value
                  type: uint16
                  endianness: little
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        format.Structs["main"].Fields[0].Endianness.Should().BeNull();
        format.Structs["main"].Fields[1].Endianness.Should().Be(Endianness.Little);
    }

    [Fact]
    public void ParsesDynamicEndianness_Expression()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: order
                  type: ascii
                  size: "2"
                - name: body
                  type: struct
                  struct: body_struct
              body_struct:
                endianness: "{order == 'II' ? 'little' : 'big'}"
                fields:
                  - name: value
                    type: uint16
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        format.Structs["body_struct"].Endianness.Should().BeNull();
        format.Structs["body_struct"].EndiannessExpression.Should().NotBeNull();
        format.Structs["body_struct"].EndiannessExpression!.OriginalText
            .Should().Be("{order == 'II' ? 'little' : 'big'}");
    }

    [Fact]
    public void ParsesDynamicEndianness_StaticStillWorks()
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

        format.Structs["main"].Endianness.Should().Be(Endianness.Little);
        format.Structs["main"].EndiannessExpression.Should().BeNull();
    }

    [Fact]
    public void ParsesMixed_OldAndNewFormat()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: header
                  type: struct
                  struct: le_header
              le_header:
                endianness: little
                fields:
                  - name: value
                    type: uint16
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        // main uses old format (list directly)
        format.Structs["main"].Endianness.Should().BeNull();
        format.Structs["main"].Fields.Should().HaveCount(1);

        // le_header uses new format (object with endianness)
        format.Structs["le_header"].Endianness.Should().Be(Endianness.Little);
        format.Structs["le_header"].Fields.Should().HaveCount(1);
    }
}
