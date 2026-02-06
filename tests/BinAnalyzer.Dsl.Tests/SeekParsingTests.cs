using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

public class SeekParsingTests
{
    [Fact]
    public void ParsesSeekConstant()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: header
                  type: uint32
                  seek: "0x3C"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[0];
        field.SeekExpression.Should().NotBeNull();
        field.SeekExpression!.OriginalText.Should().Be("0x3C");
    }

    [Fact]
    public void ParsesSeekFieldReference()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: e_lfanew
                  type: uint32
                - name: pe_sig
                  type: uint32
                  seek: "{e_lfanew}"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[1];
        field.SeekExpression.Should().NotBeNull();
        field.SeekExpression!.OriginalText.Should().Be("{e_lfanew}");
    }

    [Fact]
    public void ParsesSeekRestore()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: offset
                  type: uint32
                - name: data
                  type: uint8
                  seek: "{offset}"
                  seek_restore: true
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[1];
        field.SeekExpression.Should().NotBeNull();
        field.SeekRestore.Should().BeTrue();
    }

    [Fact]
    public void ParsesWithoutSeek()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: value
                  type: uint32
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[0];
        field.SeekExpression.Should().BeNull();
        field.SeekRestore.Should().BeFalse();
    }
}
