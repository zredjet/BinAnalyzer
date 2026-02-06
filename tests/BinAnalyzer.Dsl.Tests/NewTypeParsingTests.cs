using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

public class NewTypeParsingTests
{
    [Theory]
    [InlineData("asciiz", FieldType.AsciiZ)]
    [InlineData("utf8z", FieldType.Utf8Z)]
    public void Load_NullTerminatedStringType_ParsesCorrectly(string typeName, FieldType expected)
    {
        var yaml = $"""
            name: test
            endianness: big
            root: root
            structs:
              root:
                - name: label
                  type: {typeName}
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["root"].Fields[0];
        field.Type.Should().Be(expected);
        field.Size.Should().BeNull(); // no size needed
    }

    [Theory]
    [InlineData("float32", FieldType.Float32)]
    [InlineData("f32", FieldType.Float32)]
    [InlineData("float64", FieldType.Float64)]
    [InlineData("f64", FieldType.Float64)]
    public void Load_FloatType_ParsesCorrectly(string typeName, FieldType expected)
    {
        var yaml = $"""
            name: test
            endianness: big
            root: root
            structs:
              root:
                - name: value
                  type: {typeName}
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["root"].Fields[0];
        field.Type.Should().Be(expected);
    }

    [Theory]
    [InlineData("zlib", FieldType.Zlib)]
    [InlineData("deflate", FieldType.Deflate)]
    public void Load_CompressedType_ParsesCorrectly(string typeName, FieldType expected)
    {
        var yaml = $"""
            name: test
            endianness: big
            root: root
            structs:
              root:
                - name: data
                  type: {typeName}
                  size: "100"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["root"].Fields[0];
        field.Type.Should().Be(expected);
        field.Size.Should().Be(100);
    }
}
