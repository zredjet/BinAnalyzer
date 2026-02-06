using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

public class AlignPaddingParsingTests
{
    private readonly YamlFormatLoader _loader = new();

    [Fact]
    public void ParseField_WithAlign()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: tag
                  type: uint8
                  align: 4
                - name: value
                  type: uint32
            """;

        var format = _loader.LoadFromString(yaml);

        var tag = format.Structs["main"].Fields[0];
        tag.Align.Should().Be(4);

        var value = format.Structs["main"].Fields[1];
        value.Align.Should().BeNull();
    }

    [Fact]
    public void ParseField_WithPadding()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: data
                  type: uint32
                - name: reserved
                  type: bytes
                  size: "3"
                  padding: true
            """;

        var format = _loader.LoadFromString(yaml);

        var data = format.Structs["main"].Fields[0];
        data.IsPadding.Should().BeFalse();

        var reserved = format.Structs["main"].Fields[1];
        reserved.IsPadding.Should().BeTrue();
    }

    [Fact]
    public void ParseField_WithAlignAndPadding()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: tag
                  type: uint8
                  align: 8
                  padding: true
            """;

        var format = _loader.LoadFromString(yaml);

        var tag = format.Structs["main"].Fields[0];
        tag.Align.Should().Be(8);
        tag.IsPadding.Should().BeTrue();
    }

    [Fact]
    public void ParseField_DefaultValues()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: x
                  type: uint8
            """;

        var format = _loader.LoadFromString(yaml);

        var x = format.Structs["main"].Fields[0];
        x.Align.Should().BeNull();
        x.IsPadding.Should().BeFalse();
    }
}
