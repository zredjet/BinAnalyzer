using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class SchemaOutputTests
{
    // --- ヘルパー ---

    private static FormatDefinition CreateFormat(
        Dictionary<string, StructDefinition> structs,
        string rootStruct = "root")
    {
        return new FormatDefinition
        {
            Name = "test",
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = structs,
            RootStruct = rootStruct,
        };
    }

    private static StructDefinition Struct(string name, params FieldDefinition[] fields) =>
        new() { Name = name, Fields = fields };

    private static FieldDefinition Field(string name, FieldType type) =>
        new() { Name = name, Type = type };

    // ===========================================
    // Mermaid テスト
    // ===========================================

    [Fact]
    public void Mermaid_ContainsClassDiagramKeyword()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root", Field("value", FieldType.UInt8)),
        });

        var output = new MermaidSchemaFormatter().Format(format);

        output.Should().Contain("classDiagram");
    }

    [Fact]
    public void Mermaid_RootStructHasRootStereotype()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root", Field("value", FieldType.UInt8)),
        });

        var output = new MermaidSchemaFormatter().Format(format);

        output.Should().Contain("<<root>>");
    }

    [Fact]
    public void Mermaid_StructFieldGeneratesEdge()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "header", Type = FieldType.Struct, StructRef = "header_struct" }),
            ["header_struct"] = Struct("header_struct", Field("magic", FieldType.UInt32)),
        });

        var output = new MermaidSchemaFormatter().Format(format);

        output.Should().Contain("root --> header_struct : \"header\"");
    }

    [Fact]
    public void Mermaid_SwitchFieldGeneratesConditionalEdges()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition
                {
                    Name = "data",
                    Type = FieldType.Switch,
                    SwitchOn = ExpressionParser.Parse("{type}"),
                    SwitchCases = new List<SwitchCase>
                    {
                        new(ExpressionParser.Parse("'PNG'"), "png_data"),
                        new(ExpressionParser.Parse("'JPG'"), "jpg_data"),
                    },
                }),
            ["png_data"] = Struct("png_data", Field("width", FieldType.UInt32)),
            ["jpg_data"] = Struct("jpg_data", Field("quality", FieldType.UInt8)),
        });

        var output = new MermaidSchemaFormatter().Format(format);

        output.Should().Contain("root --> png_data : \"data ['PNG']\"");
        output.Should().Contain("root --> jpg_data : \"data ['JPG']\"");
    }

    [Fact]
    public void Mermaid_SwitchDefaultGeneratesDefaultLabel()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition
                {
                    Name = "payload",
                    Type = FieldType.Switch,
                    SwitchOn = ExpressionParser.Parse("{type}"),
                    SwitchCases = new List<SwitchCase>
                    {
                        new(ExpressionParser.Parse("1"), "type_a"),
                    },
                    SwitchDefault = "raw_data",
                }),
            ["type_a"] = Struct("type_a", Field("x", FieldType.UInt8)),
            ["raw_data"] = Struct("raw_data", Field("bytes", FieldType.Bytes)),
        });

        var output = new MermaidSchemaFormatter().Format(format);

        output.Should().Contain("root --> raw_data : \"payload [default]\"");
    }

    [Fact]
    public void Mermaid_RepeatFieldHasMultiplicity()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition
                {
                    Name = "records",
                    Type = FieldType.Struct,
                    StructRef = "record",
                    Repeat = new RepeatMode.Count(ExpressionParser.Parse("{count}")),
                }),
            ["record"] = Struct("record", Field("id", FieldType.UInt16)),
        });

        var output = new MermaidSchemaFormatter().Format(format);

        output.Should().Contain("root --> record : \"records *\"");
    }

    [Fact]
    public void Mermaid_AllFieldNamesDisplayedInClass()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                Field("magic", FieldType.UInt32),
                Field("version", FieldType.UInt16),
                Field("length", FieldType.UInt32)),
        });

        var output = new MermaidSchemaFormatter().Format(format);

        output.Should().Contain("uint32 magic");
        output.Should().Contain("uint16 version");
        output.Should().Contain("uint32 length");
    }

    [Fact]
    public void Mermaid_RepeatFieldShowsArraySuffix()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition
                {
                    Name = "entries",
                    Type = FieldType.Struct,
                    StructRef = "entry",
                    Repeat = new RepeatMode.UntilEof(),
                }),
            ["entry"] = Struct("entry", Field("v", FieldType.UInt8)),
        });

        var output = new MermaidSchemaFormatter().Format(format);

        output.Should().Contain("struct[] entries");
    }

    [Fact]
    public void Mermaid_EmptyStructGeneratesValidClass()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root"),
        });

        var output = new MermaidSchemaFormatter().Format(format);

        output.Should().Contain("class root {");
        output.Should().Contain("<<root>>");
        output.Should().Contain("}");
    }

    [Fact]
    public void Mermaid_CompressionFieldGeneratesEdgeWithLabel()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition
                {
                    Name = "compressed",
                    Type = FieldType.Zlib,
                    StructRef = "inner",
                }),
            ["inner"] = Struct("inner", Field("data", FieldType.Bytes)),
        });

        var output = new MermaidSchemaFormatter().Format(format);

        output.Should().Contain("root --> inner : \"compressed (圧縮)\"");
    }

    [Fact]
    public void Mermaid_OtfFormatIntegration()
    {
        var loader = new YamlFormatLoader();
        var format = loader.Load(Path.Combine(FindFormatsDir(), "otf.bdef.yaml"));

        var output = new MermaidSchemaFormatter().Format(format);

        output.Should().Contain("classDiagram");
        output.Should().Contain("<<root>>");
        output.Should().Contain("-->");
    }

    // ===========================================
    // DOT テスト
    // ===========================================

    [Fact]
    public void Dot_ContainsDigraphKeyword()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root", Field("value", FieldType.UInt8)),
        });

        var output = new DotSchemaFormatter().Format(format);

        output.Should().Contain("digraph schema {");
    }

    [Fact]
    public void Dot_RootStructHasBoldStyle()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root", Field("value", FieldType.UInt8)),
        });

        var output = new DotSchemaFormatter().Format(format);

        output.Should().Contain("style=bold");
        output.Should().Contain("color=blue");
    }

    [Fact]
    public void Dot_StructFieldGeneratesEdge()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition { Name = "header", Type = FieldType.Struct, StructRef = "hdr" }),
            ["hdr"] = Struct("hdr", Field("magic", FieldType.UInt32)),
        });

        var output = new DotSchemaFormatter().Format(format);

        output.Should().Contain("root -> hdr [label=\"header\"];");
    }

    [Fact]
    public void Dot_SwitchFieldGeneratesLabeledEdges()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition
                {
                    Name = "content",
                    Type = FieldType.Switch,
                    SwitchOn = ExpressionParser.Parse("{tag}"),
                    SwitchCases = new List<SwitchCase>
                    {
                        new(ExpressionParser.Parse("'A'"), "type_a"),
                    },
                    SwitchDefault = "fallback",
                }),
            ["type_a"] = Struct("type_a", Field("x", FieldType.UInt8)),
            ["fallback"] = Struct("fallback", Field("raw", FieldType.Bytes)),
        });

        var output = new DotSchemaFormatter().Format(format);

        output.Should().Contain("root -> type_a [label=");
        output.Should().Contain("root -> fallback [label=");
        output.Should().Contain("[default]");
    }

    [Fact]
    public void Dot_RepeatFieldHasMultiplicity()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "item",
                    Repeat = new RepeatMode.Count(ExpressionParser.Parse("{n}")),
                }),
            ["item"] = Struct("item", Field("id", FieldType.UInt16)),
        });

        var output = new DotSchemaFormatter().Format(format);

        output.Should().Contain("items *");
    }

    [Fact]
    public void Dot_FieldTypesDisplayCorrectly()
    {
        var format = CreateFormat(new Dictionary<string, StructDefinition>
        {
            ["root"] = Struct("root",
                Field("a", FieldType.UInt8),
                Field("b", FieldType.Ascii),
                Field("c", FieldType.Float64)),
        });

        var output = new DotSchemaFormatter().Format(format);

        output.Should().Contain("a : uint8");
        output.Should().Contain("b : ascii");
        output.Should().Contain("c : float64");
    }

    [Fact]
    public void Dot_OtfFormatIntegration()
    {
        var loader = new YamlFormatLoader();
        var format = loader.Load(Path.Combine(FindFormatsDir(), "otf.bdef.yaml"));

        var output = new DotSchemaFormatter().Format(format);

        output.Should().Contain("digraph schema {");
        output.Should().Contain("style=bold");
        output.Should().Contain("->");
        output.Should().Contain("}");
    }

    // --- ヘルパー ---

    private static string FindFormatsDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var formatsDir = Path.Combine(dir, "formats");
            if (Directory.Exists(formatsDir))
                return formatsDir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException("formats directory not found");
    }
}
