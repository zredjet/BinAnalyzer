using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

public class RepeatGuardParsingTests
{
    [Fact]
    public void ParsesRepeatMax_Constant()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: items
                  type: uint8
                  repeat: eof
                  repeat_max: "1000"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[0];
        field.RepeatMax.Should().NotBeNull();
        field.RepeatMax!.OriginalText.Should().Be("1000");
    }

    [Fact]
    public void ParsesRepeatMax_Expression()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: max_entries
                  type: uint16
                - name: items
                  type: uint8
                  repeat: eof
                  repeat_max: "{max_entries}"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[1];
        field.RepeatMax.Should().NotBeNull();
        field.RepeatMax!.OriginalText.Should().Be("{max_entries}");
    }

    [Fact]
    public void ParsesRepeatErrorLimit()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: items
                  type: uint8
                  repeat: eof
                  repeat_error_limit: "5"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[0];
        field.RepeatErrorLimit.Should().NotBeNull();
        field.RepeatErrorLimit!.OriginalText.Should().Be("5");
    }

    [Fact]
    public void ParsesBothRepeatMaxAndErrorLimit()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: items
                  type: uint8
                  repeat: eof
                  repeat_max: "100"
                  repeat_error_limit: "3"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[0];
        field.RepeatMax.Should().NotBeNull();
        field.RepeatMax!.OriginalText.Should().Be("100");
        field.RepeatErrorLimit.Should().NotBeNull();
        field.RepeatErrorLimit!.OriginalText.Should().Be("3");
    }

    [Fact]
    public void WithoutRepeatGuard_FieldsAreNull()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: items
                  type: uint8
                  repeat: eof
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[0];
        field.RepeatMax.Should().BeNull();
        field.RepeatErrorLimit.Should().BeNull();
    }

    [Fact]
    public void RepeatMax_WithCountRepeat()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: count
                  type: uint8
                - name: items
                  type: uint8
                  repeat_count: "{count}"
                  repeat_max: "50"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[1];
        field.Repeat.Should().BeOfType<RepeatMode.Count>();
        field.RepeatMax.Should().NotBeNull();
        field.RepeatMax!.OriginalText.Should().Be("50");
    }
}
