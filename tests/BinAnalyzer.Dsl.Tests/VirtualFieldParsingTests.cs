using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

public class VirtualFieldParsingTests
{
    [Fact]
    public void ParsesVirtualFieldType()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: width
                  type: uint16
                - name: height
                  type: uint16
                - name: pixel_count
                  type: virtual
                  value: "{width * height}"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var fields = format.Structs["main"].Fields;
        fields.Should().HaveCount(3);

        var virtualField = fields[2];
        virtualField.Name.Should().Be("pixel_count");
        virtualField.Type.Should().Be(FieldType.Virtual);
        virtualField.ValueExpression.Should().NotBeNull();
        virtualField.ValueExpression!.OriginalText.Should().Be("{width * height}");
    }

    [Fact]
    public void ParsesVirtualFieldWithDescription()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: value
                  type: uint32
                - name: doubled
                  type: virtual
                  value: "{value * 2}"
                  description: "倍の値"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var virtualField = format.Structs["main"].Fields[1];
        virtualField.Type.Should().Be(FieldType.Virtual);
        virtualField.Description.Should().Be("倍の値");
        virtualField.ValueExpression.Should().NotBeNull();
    }

    [Fact]
    public void ParsesVirtualFieldWithoutValue_NoExpressionSet()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: no_value
                  type: virtual
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var virtualField = format.Structs["main"].Fields[0];
        virtualField.Type.Should().Be(FieldType.Virtual);
        virtualField.ValueExpression.Should().BeNull();
    }
}
