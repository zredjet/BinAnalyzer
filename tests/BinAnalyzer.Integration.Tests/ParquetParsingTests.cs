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
        var format = new YamlFormatLoader().Load(ParquetFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ParquetFormat_DecodesMinimalParquet()
    {
        var data = ParquetTestDataGenerator.CreateMinimalParquet();
        var format = new YamlFormatLoader().Load(ParquetFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("Parquet");
        decoded.Children.Should().HaveCount(2);
        decoded.Children[0].Name.Should().Be("magic");
        decoded.Children[1].Name.Should().Be("row_groups");
    }

    [Fact]
    public void ParquetFormat_Magic_DecodesCorrectly()
    {
        var data = ParquetTestDataGenerator.CreateMinimalParquet();
        var format = new YamlFormatLoader().Load(ParquetFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var magic = decoded.Children[0].Should().BeOfType<DecodedString>().Subject;
        magic.Value.Should().Be("PAR1");
    }

    [Fact]
    public void ParquetFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = ParquetTestDataGenerator.CreateMinimalParquet();
        var format = new YamlFormatLoader().Load(ParquetFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("Parquet");
        output.Should().Contain("magic");
        output.Should().Contain("row_groups");
    }
}
