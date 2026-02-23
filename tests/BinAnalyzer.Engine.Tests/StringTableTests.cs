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
        var format = CreateFormat(tableBytes.Length, StringTableEncoding.Ascii);
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
        var format = CreateFormat(tableBytes.Length, StringTableEncoding.Ascii);
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
        var format = CreateFormat(tableBytes.Length, StringTableEncoding.Ascii);
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
        var format = CreateFormat(tableBytes.Length, StringTableEncoding.Ascii);
        var data = new byte[tableBytes.Length + 1];
        tableBytes.CopyTo(data, 0);
        data[^1] = 0; // offset 0

        var result = _decoder.Decode(data, format);

        var dataStruct = result.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var intNode = dataStruct.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        intNode.StringTableValue.Should().Be("");
    }

    [Fact]
    public void LooksUpUtf8StringFromTable()
    {
        // UTF-8 string table: "hello\0日本語\0"
        var tableBytes = System.Text.Encoding.UTF8.GetBytes("hello\0\u65E5\u672C\u8A9E\0");
        var format = CreateFormat(tableBytes.Length, StringTableEncoding.Utf8);
        var data = new byte[tableBytes.Length + 1];
        tableBytes.CopyTo(data, 0);
        data[^1] = 6; // offset past "hello\0" = 6

        var result = _decoder.Decode(data, format);

        var dataStruct = result.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var intNode = dataStruct.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        intNode.StringTableValue.Should().Be("\u65E5\u672C\u8A9E");
    }

    [Fact]
    public void LooksUpUtf16LeStringFromTable()
    {
        // UTF-16LE: "Hi\0\0Test\0\0" — two null-terminated UTF-16LE strings
        var enc = System.Text.Encoding.Unicode; // UTF-16LE
        var hiBytes = enc.GetBytes("Hi");
        var testBytes = enc.GetBytes("Test");
        var nullTerm = new byte[] { 0x00, 0x00 };

        // table = "Hi" + \0\0 + "Test" + \0\0
        var tableBytes = new byte[hiBytes.Length + 2 + testBytes.Length + 2];
        hiBytes.CopyTo(tableBytes, 0);
        nullTerm.CopyTo(tableBytes, hiBytes.Length);
        testBytes.CopyTo(tableBytes, hiBytes.Length + 2);
        nullTerm.CopyTo(tableBytes, hiBytes.Length + 2 + testBytes.Length);

        var format = CreateFormat(tableBytes.Length, StringTableEncoding.Utf16Le);
        var data = new byte[tableBytes.Length + 1];
        tableBytes.CopyTo(data, 0);
        // offset to "Test" = hiBytes.Length + 2 (null terminator)
        data[^1] = (byte)(hiBytes.Length + 2);

        var result = _decoder.Decode(data, format);

        var dataStruct = result.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var intNode = dataStruct.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        intNode.StringTableValue.Should().Be("Test");
    }

    [Fact]
    public void LooksUpUtf16BeStringFromTable()
    {
        // UTF-16BE: "AB\0\0CD\0\0"
        var enc = System.Text.Encoding.BigEndianUnicode;
        var abBytes = enc.GetBytes("AB");
        var cdBytes = enc.GetBytes("CD");
        var nullTerm = new byte[] { 0x00, 0x00 };

        var tableBytes = new byte[abBytes.Length + 2 + cdBytes.Length + 2];
        abBytes.CopyTo(tableBytes, 0);
        nullTerm.CopyTo(tableBytes, abBytes.Length);
        cdBytes.CopyTo(tableBytes, abBytes.Length + 2);
        nullTerm.CopyTo(tableBytes, abBytes.Length + 2 + cdBytes.Length);

        var format = CreateFormat(tableBytes.Length, StringTableEncoding.Utf16Be);
        var data = new byte[tableBytes.Length + 1];
        tableBytes.CopyTo(data, 0);
        data[^1] = 0; // offset 0 → "AB"

        var result = _decoder.Decode(data, format);

        var dataStruct = result.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var intNode = dataStruct.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        intNode.StringTableValue.Should().Be("AB");
    }

    [Fact]
    public void Utf16LeHandlesSingleNullByteInString()
    {
        // UTF-16LE string containing a character with one null byte (e.g. 'A' = 0x41 0x00)
        // This verifies that single 0x00 bytes within UTF-16 chars don't terminate early
        var enc = System.Text.Encoding.Unicode;
        var strBytes = enc.GetBytes("A"); // 0x41 0x00
        var nullTerm = new byte[] { 0x00, 0x00 };

        var tableBytes = new byte[strBytes.Length + 2];
        strBytes.CopyTo(tableBytes, 0);
        nullTerm.CopyTo(tableBytes, strBytes.Length);

        var format = CreateFormat(tableBytes.Length, StringTableEncoding.Utf16Le);
        var data = new byte[tableBytes.Length + 1];
        tableBytes.CopyTo(data, 0);
        data[^1] = 0;

        var result = _decoder.Decode(data, format);

        var dataStruct = result.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var intNode = dataStruct.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        intNode.StringTableValue.Should().Be("A");
    }

    /// <summary>
    /// Creates a format with a string table struct followed by a data struct.
    /// The string table struct's name field is "strtab" and the data struct references it.
    /// Layout: [strtab bytes (tableSize)] [uint8 offset]
    /// </summary>
    private static FormatDefinition CreateFormat(int tableSize, StringTableEncoding encoding = StringTableEncoding.Ascii)
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
                    StringTableEncoding = encoding,
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
