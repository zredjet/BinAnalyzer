using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class HtmlOutputTests
{
    [Fact]
    public void HtmlOutput_ContainsBasicStructure()
    {
        var root = CreateSampleStruct();
        var formatter = new HtmlOutputFormatter();

        var html = formatter.Format(root);

        html.Should().Contain("<!DOCTYPE html>");
        html.Should().Contain("<html");
        html.Should().Contain("</html>");
        html.Should().Contain("<style>");
        html.Should().Contain("<script>");
        html.Should().Contain("BinAnalyzer");
    }

    [Fact]
    public void HtmlOutput_ContainsFieldValues()
    {
        var root = CreateSampleStruct();
        var formatter = new HtmlOutputFormatter();

        var html = formatter.Format(root);

        html.Should().Contain("width");
        html.Should().Contain("100");
        html.Should().Contain("name");
        html.Should().Contain("test_file");
    }

    [Fact]
    public void HtmlOutput_ContainsToggleFunctionality()
    {
        var root = CreateSampleStruct();
        var formatter = new HtmlOutputFormatter();

        var html = formatter.Format(root);

        html.Should().Contain("toggle(this)");
        html.Should().Contain("expandAll()");
        html.Should().Contain("collapseAll()");
    }

    [Fact]
    public void HtmlOutput_ContainsEnumLabels()
    {
        var root = new DecodedStruct
        {
            Name = "test", StructType = "test", Offset = 0, Size = 1,
            Children =
            [
                new DecodedInteger { Name = "color_type", Offset = 0, Size = 1, Value = 2, EnumLabel = "truecolor", EnumDescription = "RGB" }
            ],
        };
        var formatter = new HtmlOutputFormatter();

        var html = formatter.Format(root);

        html.Should().Contain("truecolor");
        html.Should().Contain("RGB");
        html.Should().Contain("class=\"value enum\"");
    }

    [Fact]
    public void HtmlOutput_ContainsValidationStatus()
    {
        var root = new DecodedStruct
        {
            Name = "test", StructType = "test", Offset = 0, Size = 4,
            Children =
            [
                new DecodedBytes { Name = "sig", Offset = 0, Size = 4, RawBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }, ValidationPassed = true }
            ],
        };
        var formatter = new HtmlOutputFormatter();

        var html = formatter.Format(root);

        html.Should().Contain("✓");
        html.Should().Contain("class=\"valid\"");
    }

    [Fact]
    public void HtmlOutput_EscapesHtmlSpecialCharacters()
    {
        var root = new DecodedStruct
        {
            Name = "test", StructType = "test", Offset = 0, Size = 5,
            Children =
            [
                new DecodedString { Name = "value", Offset = 0, Size = 5, Value = "<b>hi</b>", Encoding = "ascii" }
            ],
        };
        var formatter = new HtmlOutputFormatter();

        var html = formatter.Format(root);

        html.Should().Contain("&lt;b&gt;");
        html.Should().NotContain("<b>hi</b>");
    }

    [Fact]
    public void HtmlOutput_SelfContained_NoExternalDependencies()
    {
        var root = CreateSampleStruct();
        var formatter = new HtmlOutputFormatter();

        var html = formatter.Format(root);

        // Should not reference external resources
        html.Should().NotContain("href=\"http");
        html.Should().NotContain("src=\"http");
        html.Should().NotContain("cdn.");
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
                new DecodedStruct
                {
                    Name = "header", StructType = "header_type", Offset = 13, Size = 7,
                    Children =
                    [
                        new DecodedInteger { Name = "version", Offset = 13, Size = 1, Value = 1 },
                        new DecodedInteger { Name = "flags", Offset = 14, Size = 2, Value = 0xFF },
                    ],
                },
            ],
        };
    }
}
