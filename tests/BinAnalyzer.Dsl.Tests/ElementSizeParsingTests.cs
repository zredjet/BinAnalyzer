using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

public class ElementSizeParsingTests
{
    private readonly YamlFormatLoader _loader = new();

    [Fact]
    public void ParseElementSize_FixedValue()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: items
                  type: uint8
                  repeat_count: "{3}"
                  element_size: "40"
            """;

        var format = _loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[0];
        field.ElementSize.Should().Be(40);
        field.ElementSizeExpression.Should().BeNull();
    }

    [Fact]
    public void ParseElementSize_Expression()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: entry_size
                  type: uint8
                - name: items
                  type: uint8
                  repeat_count: "{3}"
                  element_size: "{entry_size}"
            """;

        var format = _loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[1];
        field.ElementSize.Should().BeNull();
        field.ElementSizeExpression.Should().NotBeNull();
        field.ElementSizeExpression!.Root.Should().BeOfType<ExpressionNode.FieldReference>();
    }

    [Fact]
    public void ParseElementSize_NotSpecified()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: items
                  type: uint8
                  repeat_count: "{3}"
            """;

        var format = _loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[0];
        field.ElementSize.Should().BeNull();
        field.ElementSizeExpression.Should().BeNull();
    }

    [Fact]
    public void ParseElementSize_IntegerValue()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: items
                  type: uint8
                  repeat: eof
                  element_size: 16
            """;

        var format = _loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[0];
        field.ElementSize.Should().Be(16);
        field.ElementSizeExpression.Should().BeNull();
    }
}
