using BinAnalyzer.Core;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class FilterOutputTests
{
    [Fact]
    public void TreeOutput_FilteredTree_ShowsOnlyMatchedFields()
    {
        var format = CreateFormat();
        var data = new byte[] { 0x01, 0x00, 0x02, 0x00, 0x03 };

        var decoded = new BinaryDecoder().Decode(data, format);
        var filter = new PathFilter(["Test.header.width"]);
        var filtered = NodeFilterHelper.FilterTree(decoded, filter)!;

        var output = new TreeOutputFormatter(ColorMode.Never).Format(filtered);

        output.Should().Contain("width");
        output.Should().NotContain("height");
        output.Should().NotContain("extra");
    }

    [Fact]
    public void JsonOutput_FilteredTree_ContainsOnlyMatchedFields()
    {
        var format = CreateFormat();
        var data = new byte[] { 0x01, 0x00, 0x02, 0x00, 0x03 };

        var decoded = new BinaryDecoder().Decode(data, format);
        var filter = new PathFilter(["Test.header.width"]);
        var filtered = NodeFilterHelper.FilterTree(decoded, filter)!;

        var output = new JsonOutputFormatter().Format(filtered);

        output.Should().Contain("width");
        output.Should().NotContain("height");
        output.Should().NotContain("extra");
    }

    [Fact]
    public void CsvOutput_FilteredTree_OutputsOnlyMatchedFields()
    {
        var format = CreateFormat();
        var data = new byte[] { 0x01, 0x00, 0x02, 0x00, 0x03 };

        var decoded = new BinaryDecoder().Decode(data, format);
        var filter = new PathFilter(["Test.header.width"]);
        var filtered = NodeFilterHelper.FilterTree(decoded, filter)!;

        var output = new CsvOutputFormatter().Format(filtered);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // header + 1 field
        lines.Should().HaveCount(2);
        lines[1].Should().Contain("width");
    }

    [Fact]
    public void Filter_NoMatch_ReturnsNull()
    {
        var format = CreateFormat();
        var data = new byte[] { 0x01, 0x00, 0x02, 0x00, 0x03 };

        var decoded = new BinaryDecoder().Decode(data, format);
        var filter = new PathFilter(["nonexistent.path"]);
        var filtered = NodeFilterHelper.FilterTree(decoded, filter);

        filtered.Should().BeNull();
    }

    [Fact]
    public void Filter_DoubleWildcard_MatchesDeepFields()
    {
        var format = CreateFormat();
        var data = new byte[] { 0x01, 0x00, 0x02, 0x00, 0x03 };

        var decoded = new BinaryDecoder().Decode(data, format);
        var filter = new PathFilter(["**.width"]);
        var filtered = NodeFilterHelper.FilterTree(decoded, filter)!;

        var output = new CsvOutputFormatter().Format(filtered);

        output.Should().Contain("width");
        output.Should().NotContain("height");
        output.Should().NotContain("extra");
    }

    private static FormatDefinition CreateFormat()
    {
        return new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["main"] = new()
                {
                    Name = "main",
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "header",
                            Type = FieldType.Struct,
                            StructRef = "header",
                        },
                        new FieldDefinition
                        {
                            Name = "extra",
                            Type = FieldType.UInt8,
                        },
                    ],
                },
                ["header"] = new()
                {
                    Name = "header",
                    Fields =
                    [
                        new FieldDefinition { Name = "width", Type = FieldType.UInt16 },
                        new FieldDefinition { Name = "height", Type = FieldType.UInt16 },
                    ],
                },
            },
            RootStruct = "main",
        };
    }
}
