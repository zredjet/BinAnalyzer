using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class CsvOutputTests
{
    [Fact]
    public void Csv_HasHeaderRow()
    {
        var format = CreateFormat("main",
            new FieldDefinition { Name = "value", Type = FieldType.UInt8 });
        var data = new byte[] { 0x42 };

        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new CsvOutputFormatter().Format(decoded);

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Be("path,type,offset,size,value");
    }

    [Fact]
    public void Csv_OutputsLeafFieldsOnly()
    {
        var format = CreateFormatWithStructs(
            new Dictionary<string, StructDefinition>
            {
                ["main"] = new()
                {
                    Name = "main",
                    Fields =
                    [
                        new FieldDefinition { Name = "a", Type = FieldType.UInt8 },
                        new FieldDefinition { Name = "inner", Type = FieldType.Struct, StructRef = "inner" },
                    ],
                },
                ["inner"] = new()
                {
                    Name = "inner",
                    Fields =
                    [
                        new FieldDefinition { Name = "b", Type = FieldType.UInt16 },
                    ],
                },
            });
        var data = new byte[] { 0x01, 0x00, 0x02 };

        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new CsvOutputFormatter().Format(decoded);

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(3); // header + 2 leaf fields
        lines[1].Should().StartWith("Test.a,");
        lines[2].Should().StartWith("Test.inner.b,");
    }

    [Fact]
    public void Csv_ArrayIndexPath()
    {
        var format = CreateFormatWithStructs(
            new Dictionary<string, StructDefinition>
            {
                ["main"] = new()
                {
                    Name = "main",
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "items",
                            Type = FieldType.UInt8,
                            Repeat = new RepeatMode.Count(ExpressionParser.Parse("{3}")),
                        },
                    ],
                },
            });
        var data = new byte[] { 0x0A, 0x0B, 0x0C };

        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new CsvOutputFormatter().Format(decoded);

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // header + 3 elements
        lines.Should().HaveCount(4);
        lines[1].Should().Contain("Test.items.0");
        lines[2].Should().Contain("Test.items.1");
        lines[3].Should().Contain("Test.items.2");
    }

    [Fact]
    public void Csv_Rfc4180Escape()
    {
        // Create a string field containing a comma
        var format = CreateFormat("main",
            new FieldDefinition { Name = "text", Type = FieldType.Ascii, Size = 5 });
        var data = "he,lo"u8.ToArray();

        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new CsvOutputFormatter().Format(decoded);

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // Value containing comma should be quoted
        lines[1].Should().Contain("\"he,lo\"");
    }

    [Fact]
    public void Tsv_UsesTabSeparator()
    {
        var format = CreateFormat("main",
            new FieldDefinition { Name = "value", Type = FieldType.UInt8 });
        var data = new byte[] { 0x42 };

        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new CsvOutputFormatter(useTsv: true).Format(decoded);

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Be("path\ttype\toffset\tsize\tvalue");
        lines[1].Should().Contain("\t");
        lines[1].Should().NotContain(",");
    }

    [Fact]
    public void Csv_VirtualField_Included()
    {
        var format = CreateFormat("main",
            new FieldDefinition { Name = "a", Type = FieldType.UInt8 },
            new FieldDefinition
            {
                Name = "doubled",
                Type = FieldType.Virtual,
                ValueExpression = ExpressionParser.Parse("{a * 2}"),
            });
        var data = new byte[] { 0x05 };

        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new CsvOutputFormatter().Format(decoded);

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(3); // header + a + doubled
        lines[2].Should().Contain("virtual");
        lines[2].Should().Contain("10");
    }

    [Fact]
    public void Csv_PaddingField_Excluded()
    {
        var format = CreateFormat("main",
            new FieldDefinition { Name = "value", Type = FieldType.UInt8 },
            new FieldDefinition { Name = "pad", Type = FieldType.Bytes, Size = 2, IsPadding = true });
        var data = new byte[] { 0x01, 0x00, 0x00 };

        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new CsvOutputFormatter().Format(decoded);

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2); // header + value only (padding excluded)
    }

    private static FormatDefinition CreateFormat(string rootName, params FieldDefinition[] fields)
    {
        return new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                [rootName] = new()
                {
                    Name = rootName,
                    Fields = fields.ToList(),
                },
            },
            RootStruct = rootName,
        };
    }

    private static FormatDefinition CreateFormatWithStructs(Dictionary<string, StructDefinition> structs)
    {
        return new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = structs,
            RootStruct = "main",
        };
    }
}
