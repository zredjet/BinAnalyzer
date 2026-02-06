using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class HtmlSearchTests
{
    [Fact]
    public void HtmlOutput_ContainsSearchBar()
    {
        var root = CreateSampleStruct();
        var formatter = new HtmlOutputFormatter();

        var html = formatter.Format(root);

        html.Should().Contain("id=\"search-input\"");
        html.Should().Contain("onSearch()");
        html.Should().Contain("jumpNext()");
        html.Should().Contain("jumpPrev()");
        html.Should().Contain("id=\"search-count\"");
    }

    [Fact]
    public void HtmlOutput_ContainsSearchStyles()
    {
        var root = CreateSampleStruct();
        var formatter = new HtmlOutputFormatter();

        var html = formatter.Format(root);

        html.Should().Contain(".search-bar");
        html.Should().Contain(".search-match");
        html.Should().Contain(".search-focus");
    }

    [Fact]
    public void HtmlOutput_ContainsSearchScript()
    {
        var root = CreateSampleStruct();
        var formatter = new HtmlOutputFormatter();

        var html = formatter.Format(root);

        html.Should().Contain("function onSearch()");
        html.Should().Contain("function jumpNext()");
        html.Should().Contain("function jumpPrev()");
        html.Should().Contain("function clearSearch()");
        html.Should().Contain("saveCollapseState()");
        html.Should().Contain("restoreCollapseState()");
        html.Should().Contain("expandAncestors(");
    }

    [Fact]
    public void HtmlOutput_IntegerNode_HasDataSearchable()
    {
        var root = new DecodedStruct
        {
            Name = "test", StructType = "test", Offset = 0, Size = 4,
            Children =
            [
                new DecodedInteger { Name = "width", Offset = 0, Size = 4, Value = 256 }
            ],
        };
        var formatter = new HtmlOutputFormatter();

        var html = formatter.Format(root);

        html.Should().Contain("data-searchable=");
        html.Should().Contain("width");
        html.Should().Contain("256");
        html.Should().Contain("0x100");
    }

    [Fact]
    public void HtmlOutput_IntegerNodeWithEnum_HasEnumInSearchable()
    {
        var root = new DecodedStruct
        {
            Name = "test", StructType = "test", Offset = 0, Size = 1,
            Children =
            [
                new DecodedInteger { Name = "color", Offset = 0, Size = 1, Value = 2, EnumLabel = "truecolor" }
            ],
        };
        var formatter = new HtmlOutputFormatter();

        var html = formatter.Format(root);

        html.Should().Contain("data-searchable=");
        // The searchable text should contain the enum label
        html.Should().Contain("truecolor");
    }

    [Fact]
    public void HtmlOutput_StringNode_HasDataSearchable()
    {
        var root = new DecodedStruct
        {
            Name = "test", StructType = "test", Offset = 0, Size = 4,
            Children =
            [
                new DecodedString { Name = "type", Offset = 0, Size = 4, Value = "IHDR", Encoding = "ascii" }
            ],
        };
        var formatter = new HtmlOutputFormatter();

        var html = formatter.Format(root);

        html.Should().Contain("data-searchable=");
        html.Should().Contain("IHDR");
    }

    [Fact]
    public void HtmlOutput_BytesNode_HasHexInSearchable()
    {
        var root = new DecodedStruct
        {
            Name = "test", StructType = "test", Offset = 0, Size = 2,
            Children =
            [
                new DecodedBytes { Name = "sig", Offset = 0, Size = 2, RawBytes = new byte[] { 0x89, 0x50 } }
            ],
        };
        var formatter = new HtmlOutputFormatter();

        var html = formatter.Format(root);

        html.Should().Contain("data-searchable=");
        html.Should().Contain("89");
        html.Should().Contain("50");
    }

    [Fact]
    public void HtmlOutput_StructNode_HasDataSearchable()
    {
        var root = new DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 4,
            Children =
            [
                new DecodedStruct
                {
                    Name = "header", StructType = "ihdr", Offset = 0, Size = 4,
                    Children =
                    [
                        new DecodedInteger { Name = "x", Offset = 0, Size = 4, Value = 1 }
                    ],
                }
            ],
        };
        var formatter = new HtmlOutputFormatter();

        var html = formatter.Format(root);

        html.Should().Contain("data-searchable=");
    }

    [Fact]
    public void HtmlOutput_FloatNode_HasDataSearchable()
    {
        var root = new DecodedStruct
        {
            Name = "test", StructType = "test", Offset = 0, Size = 4,
            Children =
            [
                new DecodedFloat { Name = "ratio", Offset = 0, Size = 4, Value = 3.14, IsSinglePrecision = true }
            ],
        };
        var formatter = new HtmlOutputFormatter();

        var html = formatter.Format(root);

        html.Should().Contain("data-searchable=");
        html.Should().Contain("ratio");
        html.Should().Contain("3.14");
    }

    [Fact]
    public void HtmlOutput_KeyboardShortcuts_InScript()
    {
        var root = CreateSampleStruct();
        var formatter = new HtmlOutputFormatter();

        var html = formatter.Format(root);

        // '/' to focus search, 'Escape' to clear, 'Enter' for next
        html.Should().Contain("e.key === '/'");
        html.Should().Contain("e.key === 'Escape'");
        html.Should().Contain("e.key === 'Enter'");
    }

    private static DecodedStruct CreateSampleStruct()
    {
        return new DecodedStruct
        {
            Name = "test_format", StructType = "test_format", Offset = 0, Size = 20,
            Children =
            [
                new DecodedInteger { Name = "width", Offset = 0, Size = 4, Value = 100 },
                new DecodedString { Name = "name", Offset = 4, Size = 9, Value = "test_file", Encoding = "ascii" },
            ],
        };
    }
}
