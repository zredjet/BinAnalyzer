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
        var result = FormatValidator.Validate(new YamlFormatLoader().Load(PdfFormatPath));
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void PdfFormat_Header_DecodesVersionAndBinaryComment()
    {
        var decoded = Decode(PdfTestDataGenerator.CreateMinimalPdf());

        decoded.Child("header").Validation!.Passed.Should().BeTrue();
        decoded.Child("version").Str().Should().Be("1.4");
        decoded.Child("binary_comment").Size.Should().Be(6);
        decoded.Child("is_encrypted").Int().Should().Be(0);
    }

    [Fact]
    public void PdfFormat_MinimalPdf_DecodesObjectsXrefAndTrailer()
    {
        var data = PdfTestDataGenerator.CreateMinimalPdf();
        var revision = Decode(data).Child("revisions").Elements().Single();

        revision.Child("objects").Elements().Select(o => o.Child("object_id").Str()).Should().Equal("1 0 obj", "2 0 obj", "3 0 obj");
        revision.Child("objects").Elements()[0].Child("value").Str().Should().Contain("/Type /Catalog");
        var subsection = revision.Child("xref_table").Child("subsections").Elements().Single();
        subsection.Child("first_object").Int().Should().Be(0);
        subsection.Child("count").Int().Should().Be(4);
        var entries = subsection.Child("entries").Elements();
        entries[0].Child("kind").Str().Should().Be("f");
        entries.Skip(1).Select(e => e.Child("object_at_offset").Str().Trim()).Should().Equal("1 0 obj", "2 0 obj", "3 0 obj");
        revision.Child("trailer").Child("dictionary").Str().Should().Contain("/Root 1 0 R");
        var startxref = revision.Child("startxref");
        startxref.Child("xref_at_offset").Str().Should().StartWith("xref");
        startxref.Child("eof_marker").Validation!.Passed.Should().BeTrue();
    }

    [Fact]
    public void PdfFormat_FlateStream_IsDecompressed()
    {
        var objects = Decode(PdfTestDataGenerator.CreatePdfWithIncrementalUpdate()).Child("revisions").Elements()[0].Child("objects").Elements();

        var content = objects.Single(o => o.Child("object_id").Str() == "4 0 obj");
        content.Child("has_stream").Int().Should().Be(1);
        var stream = content.Child("stream");
        stream.Child("is_flate").Int().Should().Be(1);
        var inflated = (DecodedCompressed)stream.Child("flate_data");
        inflated.DecodedContent!.Child("content").Str().Should().Be("BT /F1 24 Tf 72 720 Td (Hello PDF) Tj ET");
    }

    [Fact]
    public void PdfFormat_IncrementalUpdate_DecodesTwoRevisions()
    {
        var revisions = Decode(PdfTestDataGenerator.CreatePdfWithIncrementalUpdate()).Child("revisions").Elements();

        revisions.Should().HaveCount(2);
        revisions[0].Child("objects").Elements().Should().HaveCount(5);
        revisions[1].Child("objects").Elements().Select(o => o.Child("object_id").Str()).Should().Equal("5 0 obj", "6 0 obj");
        var subsections = revisions[1].Child("xref_table").Child("subsections").Elements();
        subsections.Select(s => s.Child("first_object").Int()).Should().Equal(0, 5);
        revisions[1].Child("trailer").Child("dictionary").Str().Should().Contain("/Prev");
        revisions[1].Child("startxref").Child("xref_at_offset").Str().Should().StartWith("xref");
    }

    [Fact]
    public void PdfFormat_TreeOutput_ContainsExpectedElements()
    {
        var output = new TreeOutputFormatter().Format(Decode(PdfTestDataGenerator.CreateMinimalPdf()));

        output.Should().Contain("PDF");
        output.Should().Contain("revisions");
        output.Should().Contain("xref_table");
        output.Should().NotContain("peek0");
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(PdfFormatPath));
}
