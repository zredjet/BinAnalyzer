using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Presentation.Tests;

public sealed class YamlFieldLocatorTests
{
    private const string ListForm = """
        name: X
        root: png
        structs:
          png:
            - name: signature
              type: bytes
            - name: chunks
              type: struct
          # comment line
          chunk:
            - name: length   # trailing
              type: uint32
            - name: "type"
              type: ascii
        enums:
          e: []
        """;

    private const string FieldsForm = """
        structs:
          header:
            fields:
              - name: magic
                type: bytes
              - name: 'version'
                type: uint8
        """;

    [Fact]
    public void ListForm_FindsStructAndField()
    {
        var loc = YamlFieldLocator.Locate(ListForm, "chunk", "length");
        loc.Should().NotBeNull();
        loc!.StructLine.Should().Be(9);
        loc.FieldLine.Should().Be(10);
    }

    [Fact]
    public void QuotedName_AndFirstStruct()
    {
        YamlFieldLocator.Locate(ListForm, "chunk", "type")!.FieldLine.Should().Be(12);
        YamlFieldLocator.Locate(ListForm, "png", "chunks")!.FieldLine.Should().Be(6);
        YamlFieldLocator.LocateStruct(ListForm, "png").Should().Be(3);
    }

    [Fact]
    public void FieldsForm_Works()
    {
        var loc = YamlFieldLocator.Locate(FieldsForm, "header", "version");
        loc!.StructLine.Should().Be(1);
        loc.FieldLine.Should().Be(5);
    }

    [Fact]
    public void MissingStruct_ReturnsNull_MissingField_ReturnsStructOnly()
    {
        YamlFieldLocator.Locate(ListForm, "nope", "x").Should().BeNull();
        YamlFieldLocator.Locate(ListForm, "chunk", "nope").Should().Be(new YamlLocation(9, null));
        YamlFieldLocator.Locate("name: x\n", "png", "a").Should().BeNull();
        YamlFieldLocator.Locate(ListForm, "e", "x").Should().BeNull("enums 配下は探さない");
    }

    [Fact]
    public void RealPngFormat_LocatesWidth()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "png.bdef.yaml");
        var yaml = File.ReadAllText(path);
        var lines = yaml.Split('\n');
        var loc = YamlFieldLocator.Locate(yaml, "ihdr", "width");
        loc.Should().NotBeNull();
        lines[loc!.StructLine].Trim().Should().Be("ihdr:");
        lines[loc.FieldLine!.Value].Trim().Should().StartWith("- name: width");
    }
}

public sealed class YamlHighlighterTests
{
    [Fact]
    public void Tokenize_KeyStringNumberComment()
    {
        var t = YamlHighlighter.Tokenize("    - name: \"type\"  # 4 bytes 0x10");
        t.Should().Contain(x => x.Kind == YamlTokenKind.Key && x.Text == "name");
        t.Should().Contain(x => x.Kind == YamlTokenKind.String && x.Text == "\"type\"");
        t.Should().Contain(x => x.Kind == YamlTokenKind.Comment && x.Text == "# 4 bytes 0x10");
        string.Concat(t.Select(x => x.Text)).Should().Be("    - name: \"type\"  # 4 bytes 0x10");
    }

    [Fact]
    public void Tokenize_Numbers_AndPlainValues()
    {
        var t = YamlHighlighter.Tokenize("size: 8");
        t.Should().Contain(x => x.Kind == YamlTokenKind.Number && x.Text == "8");
        var t2 = YamlHighlighter.Tokenize("type: uint32");
        t2.Should().Contain(x => x.Kind == YamlTokenKind.Plain && x.Text == "uint32");
        t2.Should().Contain(x => x.Kind == YamlTokenKind.Key && x.Text == "type");
    }
}
