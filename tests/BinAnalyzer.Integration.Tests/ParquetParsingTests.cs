using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class ParquetParsingTests
{
    private static readonly string ParquetFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "parquet.bdef.yaml");

    [Fact]
    public void ParquetFormat_LoadsWithoutErrors()
    {
        var result = FormatValidator.Validate(new YamlFormatLoader().Load(ParquetFormatPath));
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ParquetFormat_MagicAndFooterLength_DecodeCorrectly()
    {
        var data = ParquetTestDataGenerator.CreateTwoColumnParquet();
        var decoded = Decode(data);

        decoded.Child("magic").Validation!.Passed.Should().BeTrue();
        decoded.Child("footer_magic").Validation!.Passed.Should().BeTrue();
        var footerLength = decoded.Child("footer_length").Int();
        decoded.Child("footer").Offset.Should().Be(data.Length - 8 - footerLength);
        decoded.Child("footer").Size.Should().Be(footerLength);
    }

    [Fact]
    public void ParquetFormat_FileMetaData_NamesFieldsAndValues()
    {
        var footer = Decode(ParquetTestDataGenerator.CreateTwoColumnParquet()).Child("footer");

        footer.Child("struct_kind").Label().Should().Be("FileMetaData");
        var fields = Fields(footer);
        fields.Keys.Should().Equal("version", "schema", "num_rows", "row_groups", "key_value_metadata", "created_by");
        Number(fields["version"]).Should().Be(1);
        Number(fields["num_rows"]).Should().Be(3);
        Text(fields["created_by"]).Should().Be("binanalyzer-test");

        var keyValue = Fields(Elements(fields["key_value_metadata"]).Single());
        Text(keyValue["key"]).Should().Be("origin");
        Text(keyValue["value"]).Should().Be("binanalyzer");
    }

    [Fact]
    public void ParquetFormat_Schema_DecodesElementsWithEnums()
    {
        var footer = Decode(ParquetTestDataGenerator.CreateTwoColumnParquet()).Child("footer");

        var schema = Elements(Fields(footer)["schema"]).Select(Fields).ToList();
        schema.Select(e => Text(e["name"])).Should().Equal("schema", "id", "name");
        Number(schema[0]["num_children"]).Should().Be(2);
        schema[1]["type"].Child("value").Child("parquet_type").Label().Should().Be("INT32");
        schema[1]["repetition_type"].Child("value").Child("repetition").Label().Should().Be("REQUIRED");
        schema[2]["type"].Child("value").Child("parquet_type").Label().Should().Be("BYTE_ARRAY");
        schema[2]["converted_type"].Child("value").Child("converted").Label().Should().Be("UTF8");
    }

    [Fact]
    public void ParquetFormat_ColumnMetaData_FollowsDataPageOffsetToPageHeader()
    {
        var footer = Decode(ParquetTestDataGenerator.CreateTwoColumnParquet()).Child("footer");

        var rowGroup = Fields(Elements(Fields(footer)["row_groups"]).Single());
        Number(rowGroup["num_rows"]).Should().Be(3);
        var columns = Elements(rowGroup["columns"]).Select(c => Fields(Fields(c)["meta_data"].Child("value"))).ToList();
        columns.Should().HaveCount(2);
        Elements(columns[1]["path_in_schema"]).Select(e => e.Child("text").Str()).Should().Equal("name");
        columns[0]["codec"].Child("value").Child("codec").Label().Should().Be("UNCOMPRESSED");
        Elements(columns[0]["encodings"]).Select(e => e.Child("encoding").Label()).Should().Equal("PLAIN", "RLE");
        Number(columns[0]["data_page_offset"]).Should().Be(4);

        var pageHeader = columns[0]["data_page_offset"].Child("page_header");
        pageHeader.Child("struct_kind").Label().Should().Be("PageHeader");
        var page = Fields(pageHeader);
        page["type"].Child("value").Child("page_type").Label().Should().Be("DATA_PAGE");
        Number(page["compressed_page_size"]).Should().Be(12);
        var dataPage = Fields(page["data_page_header"].Child("value"));
        Number(dataPage["num_values"]).Should().Be(3);
        dataPage["encoding"].Child("value").Child("encoding").Label().Should().Be("PLAIN");
    }

    [Fact]
    public void ParquetFormat_TreeOutput_HidesWorkingValues()
    {
        var output = new TreeOutputFormatter().Format(Decode(ParquetTestDataGenerator.CreateTwoColumnParquet()));

        output.Should().Contain("FileMetaData");
        output.Should().Contain("row_groups");
        output.Should().NotContain("own_kind");
        output.Should().NotContain("value_enum");
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(ParquetFormatPath));

    /// <summary>Thrift の構造体のフィールドを名前（表に無ければ番号）から引く辞書にする。</summary>
    private static Dictionary<string, DecodedNode> Fields(DecodedNode thriftStruct)
    {
        if (thriftStruct.Name is "item" or "value")
            thriftStruct = thriftStruct is DecodedStruct { StructType: "thrift_element" } ? thriftStruct.Child("item") : thriftStruct;
        var result = new Dictionary<string, DecodedNode>();
        foreach (var field in thriftStruct.Child("fields").Elements())
        {
            var name = ((DecodedStruct)field).Children.FirstOrDefault(c => c.Name == "field");
            if (name is not null)
                result[name.Label() ?? name.Int().ToString()] = field;
        }
        return result;
    }

    private static IReadOnlyList<DecodedNode> Elements(DecodedNode field) =>
        field.Child("value").Child("elements").Elements().Select(e => e.Child("item")).ToList();

    private static long Number(DecodedNode field) => field.Child("value").Child("number").Int();

    private static string Text(DecodedNode field) => field.Child("value").Child("text").Str();
}
