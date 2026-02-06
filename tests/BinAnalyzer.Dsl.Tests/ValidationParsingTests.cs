using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

public class ValidationParsingTests
{
    [Fact]
    public void ParsesValidateExpression()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: magic
                  type: uint16
                  validate: "{magic == 42}"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[0];
        field.ValidationExpression.Should().NotBeNull();
        field.ValidationExpression!.OriginalText.Should().Be("{magic == 42}");
    }

    [Fact]
    public void ParsesWithoutValidate()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: value
                  type: uint8
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[0];
        field.ValidationExpression.Should().BeNull();
    }

    [Fact]
    public void ParsesValidateWithStringComparison()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: byte_order
                  type: ascii
                  size: "2"
                  validate: "{byte_order == 'II'}"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[0];
        field.ValidationExpression.Should().NotBeNull();
        field.ValidationExpression!.OriginalText.Should().Be("{byte_order == 'II'}");
    }
}
