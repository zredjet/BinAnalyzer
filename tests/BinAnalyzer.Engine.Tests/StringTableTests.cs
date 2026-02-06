using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class StringTableTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void LooksUpStringFromTable()
    {
        // string table: "hello\0world\0" (12 bytes), then uint8 offset=0 → "hello"
        var tableStr = "hello\0world\0";
        var tableBytes = System.Text.Encoding.ASCII.GetBytes(tableStr);
        var format = CreateFormat(tableBytes.Length);
        var data = new byte[tableBytes.Length + 1];
        tableBytes.CopyTo(data, 0);
        data[^1] = 0; // offset 0

        var result = _decoder.Decode(data, format);

        var stStruct = result.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        var dataStruct = result.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var intNode = dataStruct.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        intNode.Value.Should().Be(0);
        intNode.StringTableValue.Should().Be("hello");
    }

    [Fact]
    public void LooksUpStringAtNonZeroOffset()
    {
        // offset 6 → "world"
        var tableStr = "hello\0world\0";
        var tableBytes = System.Text.Encoding.ASCII.GetBytes(tableStr);
        var format = CreateFormat(tableBytes.Length);
        var data = new byte[tableBytes.Length + 1];
        tableBytes.CopyTo(data, 0);
        data[^1] = 6; // offset 6

        var result = _decoder.Decode(data, format);

        var dataStruct = result.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var intNode = dataStruct.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        intNode.Value.Should().Be(6);
        intNode.StringTableValue.Should().Be("world");
    }

    [Fact]
    public void ReturnsNullForOutOfRangeOffset()
    {
        var tableStr = "abc\0";
        var tableBytes = System.Text.Encoding.ASCII.GetBytes(tableStr);
        var format = CreateFormat(tableBytes.Length);
        var data = new byte[tableBytes.Length + 1];
        tableBytes.CopyTo(data, 0);
        data[^1] = 100; // out of range

        var result = _decoder.Decode(data, format);

        var dataStruct = result.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var intNode = dataStruct.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        intNode.StringTableValue.Should().BeNull();
    }

    [Fact]
    public void ReturnsNullForUnknownTable()
    {
        var format = new FormatDefinition
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
                            Name = "ref_field",
                            Type = FieldType.UInt8,
                            StringTableRef = "nonexistent",
                        },
                    ],
                },
            },
            RootStruct = "main",
        };

        var data = new byte[] { 0x05 };
        var result = _decoder.Decode(data, format);

        var intNode = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        intNode.Value.Should().Be(5);
        intNode.StringTableValue.Should().BeNull();
    }

    [Fact]
    public void EmptyStringAtOffset()
    {
        // "\0hello\0" — offset 0 → empty string
        var tableStr = "\0hello\0";
        var tableBytes = System.Text.Encoding.ASCII.GetBytes(tableStr);
        var format = CreateFormat(tableBytes.Length);
        var data = new byte[tableBytes.Length + 1];
        tableBytes.CopyTo(data, 0);
        data[^1] = 0; // offset 0

        var result = _decoder.Decode(data, format);

        var dataStruct = result.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var intNode = dataStruct.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        intNode.StringTableValue.Should().Be("");
    }

    /// <summary>
    /// Creates a format with a string table struct followed by a data struct.
    /// The string table struct's name field is "strtab" and the data struct references it.
    /// Layout: [strtab bytes (tableSize)] [uint8 offset]
    /// </summary>
    private static FormatDefinition CreateFormat(int tableSize)
    {
        return new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["root"] = new()
                {
                    Name = "root",
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "strtab",
                            Type = FieldType.Struct,
                            StructRef = "strtab_struct",
                        },
                        new FieldDefinition
                        {
                            Name = "data",
                            Type = FieldType.Struct,
                            StructRef = "data_struct",
                        },
                    ],
                },
                ["strtab_struct"] = new()
                {
                    Name = "strtab_struct",
                    IsStringTable = true,
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "content",
                            Type = FieldType.Bytes,
                            Size = tableSize,
                        },
                    ],
                },
                ["data_struct"] = new()
                {
                    Name = "data_struct",
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "ref_field",
                            Type = FieldType.UInt8,
                            StringTableRef = "strtab",
                        },
                    ],
                },
            },
            RootStruct = "root",
        };
    }
}
