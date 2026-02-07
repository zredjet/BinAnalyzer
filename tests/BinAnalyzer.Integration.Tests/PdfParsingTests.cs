using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class PdfParsingTests
{
    private static readonly string PdfFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "pdf.bdef.yaml");

    [Fact]
    public void PdfFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(PdfFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void PdfFormat_DecodesMinimalPdf()
    {
        var pdfData = PdfTestDataGenerator.CreateMinimalPdf();
        var format = new YamlFormatLoader().Load(PdfFormatPath);
        var decoded = new BinaryDecoder().Decode(pdfData, format);

        decoded.Name.Should().Be("PDF");
        decoded.Children.Should().HaveCount(3);
        decoded.Children[0].Name.Should().Be("version");
        decoded.Children[1].Name.Should().Be("binary_comment");
        decoded.Children[2].Name.Should().Be("body");
    }

    [Fact]
    public void PdfFormat_Version_DecodesCorrectly()
    {
        var pdfData = PdfTestDataGenerator.CreateMinimalPdf();
        var format = new YamlFormatLoader().Load(PdfFormatPath);
        var decoded = new BinaryDecoder().Decode(pdfData, format);

        var version = decoded.Children[0].Should().BeOfType<DecodedString>().Subject;
        version.Value.Should().Be("%PDF-1.4");
    }

    [Fact]
    public void PdfFormat_TreeOutput_ContainsExpectedElements()
    {
        var pdfData = PdfTestDataGenerator.CreateMinimalPdf();
        var format = new YamlFormatLoader().Load(PdfFormatPath);
        var decoded = new BinaryDecoder().Decode(pdfData, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("PDF");
        output.Should().Contain("version");
        output.Should().Contain("binary_comment");
        output.Should().Contain("body");
    }
}
