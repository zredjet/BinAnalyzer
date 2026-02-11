using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class SqliteParsingTests
{
    private static readonly string SqliteFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "sqlite.bdef.yaml");

    [Fact]
    public void SqliteFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(SqliteFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void SqliteFormat_DecodesMinimalSqlite()
    {
        var data = SqliteTestDataGenerator.CreateMinimalSqlite();
        var format = new YamlFormatLoader().Load(SqliteFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("SQLite");
        decoded.Children.Should().HaveCount(2);
        decoded.Children[0].Name.Should().Be("header");
        decoded.Children[1].Name.Should().Be("first_page");
    }

    [Fact]
    public void SqliteFormat_Header_DecodesCorrectly()
    {
        var data = SqliteTestDataGenerator.CreateMinimalSqlite();
        var format = new YamlFormatLoader().Load(SqliteFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var header = decoded.Children[0].Should().BeOfType<DecodedStruct>().Subject;

        var pageSize = header.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        pageSize.Name.Should().Be("page_size");
        pageSize.Value.Should().Be(4096);

        var maxEmbedded = header.Children[5].Should().BeOfType<DecodedInteger>().Subject;
        maxEmbedded.Value.Should().Be(64);

        var textEncoding = header.Children[16].Should().BeOfType<DecodedInteger>().Subject;
        textEncoding.Name.Should().Be("text_encoding");
        textEncoding.EnumLabel.Should().Be("UTF-8");
    }

    [Fact]
    public void SqliteFormat_BtreePage_DecodesCorrectly()
    {
        var data = SqliteTestDataGenerator.CreateMinimalSqlite();
        var format = new YamlFormatLoader().Load(SqliteFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var firstPage = decoded.Children[1].Should().BeOfType<DecodedStruct>().Subject;

        var pageType = firstPage.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        pageType.Name.Should().Be("page_type");
        pageType.Value.Should().Be(13);
        pageType.EnumLabel.Should().Be("TABLE_LEAF");
    }

    [Fact]
    public void SqliteFormat_TableLeafCell_DecodesCorrectly()
    {
        var data = SqliteTestDataGenerator.CreateSqliteWithCell();
        var format = new YamlFormatLoader().Load(SqliteFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var firstPage = decoded.Children[1].Should().BeOfType<DecodedStruct>().Subject;

        // cells should be present as the last child (after cell_pointer_array)
        var cells = firstPage.Children.Last().Should().BeOfType<DecodedArray>().Subject;
        cells.Name.Should().Be("cells");
        cells.Elements.Should().HaveCount(1);

        var cell = cells.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        var payloadSize = cell.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        payloadSize.Name.Should().Be("payload_size");
        payloadSize.Value.Should().Be(2);

        var rowid = cell.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        rowid.Name.Should().Be("rowid");
        rowid.Value.Should().Be(1);

        var payload = cell.Children[2].Should().BeOfType<DecodedBytes>().Subject;
        payload.Name.Should().Be("payload");
        payload.Size.Should().Be(2);
    }

    [Fact]
    public void SqliteFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = SqliteTestDataGenerator.CreateMinimalSqlite();
        var format = new YamlFormatLoader().Load(SqliteFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("SQLite");
        output.Should().Contain("header");
        output.Should().Contain("page_size");
        output.Should().Contain("UTF-8");
        output.Should().Contain("TABLE_LEAF");
    }
}
