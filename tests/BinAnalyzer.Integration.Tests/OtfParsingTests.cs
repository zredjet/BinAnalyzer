using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class OtfParsingTests
{
    private static readonly string OtfFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "otf.bdef.yaml");

    [Fact]
    public void OtfFormat_LoadsWithoutErrors()
    {
        var result = FormatValidator.Validate(new YamlFormatLoader().Load(OtfFormatPath));
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void OtfFormat_MinimalOtf_DecodesOffsetTableAndHead()
    {
        var decoded = Decode(OtfTestDataGenerator.CreateMinimalOtf());

        decoded.Child("offset_table").Child("sfVersion").Label().Should().Be("CFF");
        var head = Tables(decoded)["head"];
        head.Child("magicNumber").Validation!.Passed.Should().BeTrue();
        head.Child("unitsPerEm").Int().Should().BeGreaterThan(0);
    }

    [Fact]
    public void OtfFormat_MinimalTtf_IsTrueType()
    {
        Decode(OtfTestDataGenerator.CreateMinimalTtf()).Child("offset_table").Child("sfVersion").Label().Should().Be("TrueType");
    }

    [Fact]
    public void OtfFormat_NameTable_DecodesStringsByPlatform()
    {
        var name = Tables(Decode(OtfTestDataGenerator.CreateTtfWithTables()))["name"];

        var records = name.Child("records").Elements();
        records.Select(r => r.Child("text").Str()).Should().Equal("Test Sans", "Test Sans", "Regular", "テスト サンス", "Weight");
        records[0].Child("platformID").Label().Should().Be("Macintosh");
        records[3].Child("nameID").Label().Should().Be("FullName");
        records[3].Child("languageID").Int().Should().Be(0x0411);
    }

    [Fact]
    public void OtfFormat_Cmap_DecodesFormat4And12()
    {
        var cmap = Tables(Decode(OtfTestDataGenerator.CreateTtfWithTables()))["cmap"];

        var subtables = cmap.Child("encoding_records").Elements().Select(r => r.Child("subtable")).ToList();
        subtables.Select(s => s.Child("format").Int()).Should().Equal(4, 12);
        var format4 = subtables[0].Child("body");
        format4.Child("segCount").Int().Should().Be(2);
        format4.Child("startCode").Elements().Select(e => e.Int()).Should().Equal(0x41, 0xFFFF);
        format4.Child("endCode").Elements().Select(e => e.Int()).Should().Equal(0x43, 0xFFFF);
        format4.Child("idDelta").Elements()[0].Int().Should().Be(-0x40);
        var group = subtables[1].Child("body").Child("groups").Elements().Single();
        group.Child("startCharCode").Int().Should().Be(0x1F600);
        group.Child("startGlyphID").Int().Should().Be(4);
    }

    [Fact]
    public void OtfFormat_MetricsAndVariationTables_Decode()
    {
        var tables = Tables(Decode(OtfTestDataGenerator.CreateTtfWithTables()));

        tables["maxp"].Child("numGlyphs").Int().Should().Be(5);
        tables["hhea"].Child("ascender").Int().Should().Be(800);
        tables["hhea"].Child("descender").Int().Should().Be(-200);
        var os2 = tables["OS/2"];
        os2.Child("version").Int().Should().Be(4);
        os2.Child("achVendID").Str().Should().Be("TEST");
        os2.Child("sCapHeight").Int().Should().Be(700);
        tables["post"].Child("names").Elements().Single().Child("text").Str().Should().Be("smileyfc");
        var axis = tables["fvar"].Child("axes").Elements().Single();
        axis.Child("axisTag").Str().Should().Be("wght");
        axis.Child("maxValue").Int().Should().Be(900 << 16);
        tables["fvar"].Child("instances").Elements().Select(i => i.Child("coordinates").Elements().Single().Int()).Should().Equal(400 << 16, 700 << 16);
        var gsub = tables["GSUB"];
        gsub.Child("script_list").Child("records").Elements().Single().Child("tag").Str().Should().Be("latn");
        gsub.Child("feature_list").Child("records").Elements().Single().Child("tag").Str().Should().Be("liga");
    }

    [Fact]
    public void OtfFormat_Collection_DecodesEachFontWithSharedTables()
    {
        var collection = Decode(OtfTestDataGenerator.CreateTtc()).Child("collection");

        collection.Child("ttc_tag").Str().Should().Be("ttcf");
        collection.Child("numFonts").Int().Should().Be(2);
        var fonts = collection.Child("fonts").Elements();
        fonts.Should().HaveCount(2);
        var first = fonts[0].Child("table_records").Elements();
        var second = fonts[1].Child("table_records").Elements();
        second.Select(r => r.Child("offset").Int()).Should().Equal(first.Select(r => r.Child("offset").Int()));
        second.Single(r => r.Child("tag").Str() == "name").Child("table_data").Child("records").Elements()[2].Child("text").Str().Should().Be("Regular");
    }

    [Fact]
    public void OtfFormat_TreeOutput_ContainsExpectedElements()
    {
        var output = new TreeOutputFormatter().Format(Decode(OtfTestDataGenerator.CreateTtfWithTables()));

        output.Should().Contain("TrueType");
        output.Should().Contain("テスト サンス");
        output.Should().NotContain("sfnt_tag");
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(OtfFormatPath));

    private static Dictionary<string, DecodedNode> Tables(DecodedStruct root) =>
        root.Child("table_records").Elements().ToDictionary(r => r.Child("tag").Str(), r => r.Child("table_data"));
}
