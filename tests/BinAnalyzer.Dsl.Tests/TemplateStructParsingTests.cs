using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

public class TemplateStructParsingTests
{
    [Fact]
    public void ParsesTemplateDefinition_WithDefaults()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              tlv(tag_size=1, len_size=1):
                - name: tag
                  type: uint8
                  size: "{tag_size}"
                - name: length
                  type: uint8
                  size: "{len_size}"
                - name: value
                  type: bytes
                  size: "{length}"
              main:
                - name: entry
                  type: struct
                  struct: tlv(tag_size=2, len_size=2)
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        format.Structs.Should().ContainKey("tlv");
        var tlv = format.Structs["tlv"];
        tlv.Parameters.Should().HaveCount(2);
        tlv.Parameters[0].Name.Should().Be("tag_size");
        tlv.Parameters[0].DefaultValue.Should().Be(1);
        tlv.Parameters[1].Name.Should().Be("len_size");
        tlv.Parameters[1].DefaultValue.Should().Be(1);
    }

    [Fact]
    public void ParsesTemplateDefinition_WithoutDefaults()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              record(size):
                - name: data
                  type: bytes
                  size: "{size}"
              main:
                - name: entry
                  type: struct
                  struct: record(4)
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        format.Structs.Should().ContainKey("record");
        var record = format.Structs["record"];
        record.Parameters.Should().HaveCount(1);
        record.Parameters[0].Name.Should().Be("size");
        record.Parameters[0].DefaultValue.Should().BeNull();
    }

    [Fact]
    public void ParsesTemplateDefinition_MixedDefaults()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              chunk(id_size, data_size=4):
                - name: id
                  type: bytes
                  size: "{id_size}"
                - name: data
                  type: bytes
                  size: "{data_size}"
              main:
                - name: entry
                  type: struct
                  struct: chunk(2)
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var chunk = format.Structs["chunk"];
        chunk.Parameters.Should().HaveCount(2);
        chunk.Parameters[0].Name.Should().Be("id_size");
        chunk.Parameters[0].DefaultValue.Should().BeNull();
        chunk.Parameters[1].Name.Should().Be("data_size");
        chunk.Parameters[1].DefaultValue.Should().Be(4);
    }

    [Fact]
    public void ParsesTemplateRef_NamedArgs()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              tlv(tag_size=1, len_size=1):
                - name: tag
                  type: uint8
                  size: "{tag_size}"
                - name: length
                  type: uint8
                  size: "{len_size}"
                - name: value
                  type: bytes
                  size: "{length}"
              main:
                - name: entry
                  type: struct
                  struct: tlv(tag_size=1, len_size=2)
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[0];
        field.StructRef.Should().Be("tlv");
        field.StructArgs.Should().NotBeNull();
        field.StructArgs.Should().HaveCount(2);
        field.StructArgs![0].ParameterName.Should().Be("tag_size");
        field.StructArgs[0].Value.Should().Be(1);
        field.StructArgs[1].ParameterName.Should().Be("len_size");
        field.StructArgs[1].Value.Should().Be(2);
    }

    [Fact]
    public void ParsesTemplateRef_PositionalArgs()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              tlv(tag_size=1, len_size=1):
                - name: tag
                  type: uint8
                  size: "{tag_size}"
                - name: length
                  type: uint8
                  size: "{len_size}"
                - name: value
                  type: bytes
                  size: "{length}"
              main:
                - name: entry
                  type: struct
                  struct: tlv(1, 2)
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[0];
        field.StructRef.Should().Be("tlv");
        field.StructArgs.Should().HaveCount(2);
        field.StructArgs![0].ParameterName.Should().BeNull();
        field.StructArgs[0].Value.Should().Be(1);
        field.StructArgs[1].ParameterName.Should().BeNull();
        field.StructArgs[1].Value.Should().Be(2);
    }

    [Fact]
    public void ParsesTemplateRef_ExpressionArgs()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              record(size=1):
                - name: data
                  type: bytes
                  size: "{size}"
              main:
                - name: header_size
                  type: uint8
                - name: entry
                  type: struct
                  struct: "record(size={header_size})"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[1];
        field.StructRef.Should().Be("record");
        field.StructArgs.Should().HaveCount(1);
        field.StructArgs![0].ParameterName.Should().Be("size");
        field.StructArgs[0].Expression.Should().NotBeNull();
        field.StructArgs[0].Value.Should().BeNull();
    }

    [Fact]
    public void ParsesNonTemplate_Unchanged()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              simple:
                - name: value
                  type: uint8
              main:
                - name: entry
                  type: struct
                  struct: simple
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var simple = format.Structs["simple"];
        simple.Parameters.Should().BeEmpty();

        var field = format.Structs["main"].Fields[0];
        field.StructRef.Should().Be("simple");
        field.StructArgs.Should().BeNull();
    }
}
