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
        var result = FormatValidator.Validate(new YamlFormatLoader().Load(SqliteFormatPath));
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void SqliteFormat_Header_DecodesCorrectly()
    {
        var header = Decode(SqliteTestDataGenerator.CreateMinimalSqlite()).Child("header");

        header.Child("magic").Validation!.Passed.Should().BeTrue();
        header.Child("page_size_bytes").Int().Should().Be(4096);
        header.Child("text_encoding").Label().Should().Be("UTF-8");
        header.Child("write_version").Label().Should().Be("legacy");
    }

    [Fact]
    public void SqliteFormat_MinimalSqlite_DecodesFirstPage()
    {
        var page = Decode(SqliteTestDataGenerator.CreateMinimalSqlite()).Child("pages").Elements().Single();

        page.Child("page_number").Int().Should().Be(1);
        page.Child("header_area").Size.Should().Be(100);
        page.Child("btree").Child("page_type").Label().Should().Be("TABLE_LEAF");
    }

    [Fact]
    public void SqliteFormat_Records_DecodeSerialTypes()
    {
        var decoded = Decode(SqliteTestDataGenerator.CreateSqliteWithRecords());

        var pages = decoded.Child("pages").Elements();
        pages.Should().HaveCount(3);
        var schema = Values(pages[0].Child("btree").Child("cells").Elements().Single());
        schema.Select(v => v.Child("kind").Str()).Should().Equal("TEXT", "TEXT", "TEXT", "INTEGER", "TEXT");
        schema[4].Child("text_value").Str().Should().Be("CREATE TABLE t(a, b, c, d, e)");

        var rows = pages[1].Child("btree").Child("cells").Elements();
        rows.Select(r => r.Child("rowid").Int()).Should().Equal(1, 2, 3);
        var row1 = Values(rows[0]);
        row1[0].Child("kind").Str().Should().Be("NULL");
        row1[1].Child("int8_value").Int().Should().Be(-1);
        row1[2].Child("int24_value").Int().Should().Be(70000);
        row1[3].Child("text_value").Str().Should().Be("abc");
        ((DecodedFloat)row1[4].Child("real_value")).Value.Should().Be(1.5);
        var row2 = Values(rows[1]);
        row2[0].Child("constant_value").Int().Should().Be(0);
        row2[1].Child("constant_value").Int().Should().Be(1);
        row2[2].Child("int48_value").Int().Should().Be(1L << 40);
        ((DecodedBytes)row2[3].Child("blob_value")).RawBytes.ToArray().Should().Equal(0x00, 0xFF);
        row2[4].Child("text_value").Str().Should().Be("こんにちは");
    }

    [Fact]
    public void SqliteFormat_OverflowingPayload_KeepsLocalPartAndOverflowPage()
    {
        var pages = Decode(SqliteTestDataGenerator.CreateSqliteWithRecords()).Child("pages").Elements();

        var cell = pages[1].Child("btree").Child("cells").Elements()[2];
        cell.Child("payload_size").Int().Should().Be(611);
        cell.Child("local_size").Int().Should().Be(103);   // M + (P - M) % (U - 4)
        cell.Child("local_payload").Size.Should().Be(103);
        cell.Child("overflow_page").Int().Should().Be(3);
        // あふれページは B 木でないページとして、先頭 4 バイト（次のページ番号 = 0）を示す
        pages[2].Child("other").Child("next_page").Int().Should().Be(0);
    }

    [Fact]
    public void SqliteFormat_TreeOutput_ContainsExpectedElements()
    {
        var output = new TreeOutputFormatter().Format(Decode(SqliteTestDataGenerator.CreateSqliteWithRecords()));

        output.Should().Contain("SQLite");
        output.Should().Contain("TABLE_LEAF");
        output.Should().Contain("CREATE TABLE t");
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(SqliteFormatPath));

    private static IReadOnlyList<DecodedNode> Values(DecodedNode cell) => cell.Child("record").Child("values").Elements();
}
