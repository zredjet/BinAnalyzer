using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Presentation;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Presentation.Tests;

public sealed class NodeDisplayTextTests
{
    [Fact]
    public void For_Struct_SameName_ReturnsName()
    {
        var node = new DecodedStruct
        {
            Name = "header",
            StructType = "header",
            Offset = 0,
            Size = 10,
            Children = [],
        };

        NodeDisplayText.For(node).Should().Be("header");
    }

    [Fact]
    public void For_Struct_DifferentType_ReturnsNameAndType()
    {
        var node = new DecodedStruct
        {
            Name = "ihdr",
            StructType = "IHDR",
            Offset = 0,
            Size = 13,
            Children = [],
        };

        NodeDisplayText.For(node).Should().Be("ihdr \u2192 IHDR");
    }

    [Fact]
    public void For_Integer_ReturnsNameAndValue()
    {
        var node = new DecodedInteger
        {
            Name = "width",
            Offset = 0,
            Size = 4,
            Value = 800,
        };

        NodeDisplayText.For(node).Should().Be("width: 800");
    }

    [Fact]
    public void For_Integer_WithEnum_IncludesLabel()
    {
        var node = new DecodedInteger
        {
            Name = "type",
            Offset = 0,
            Size = 1,
            Value = 2,
            EnumLabel = "RGB",
        };

        NodeDisplayText.For(node).Should().Be("type: 2 \"RGB\"");
    }

    [Fact]
    public void For_Array_ShowsCount()
    {
        var node = new DecodedArray
        {
            Name = "chunks",
            Offset = 0,
            Size = 100,
            Elements =
            [
                new DecodedStruct { Name = "c0", StructType = "Chunk", Offset = 0, Size = 50, Children = [] },
                new DecodedStruct { Name = "c1", StructType = "Chunk", Offset = 50, Size = 50, Children = [] },
            ],
        };

        NodeDisplayText.For(node).Should().Be("chunks [2 items]");
    }

    [Fact]
    public void For_String_ShowsQuotedValue()
    {
        var node = new DecodedString
        {
            Name = "sig",
            Offset = 0,
            Size = 3,
            Value = "PNG",
            Encoding = "ASCII",
        };

        NodeDisplayText.For(node).Should().Be("sig: \"PNG\"");
    }

    [Fact]
    public void For_Error_ShowsErrorWithMarker()
    {
        var node = new DecodedError
        {
            Name = "bad",
            Offset = 0,
            Size = 0,
            ErrorMessage = "unexpected EOF",
        };

        NodeDisplayText.For(node).Should().Be("\u2717 bad: unexpected EOF");
    }

    [Fact]
    public void ValueOnly_Integer_WithEnum_OmitsName()
    {
        var node = new DecodedInteger { Name = "type", Offset = 0, Size = 1, Value = 2, EnumLabel = "RGB" };
        NodeDisplayText.ValueOnly(node).Should().Be("2 \"RGB\"");
    }

    [Fact]
    public void ValueOnly_Struct_IsEmpty()
    {
        var node = new DecodedStruct { Name = "s", StructType = "S", Offset = 0, Size = 0, Children = [] };
        NodeDisplayText.ValueOnly(node).Should().BeEmpty();
    }

    [Theory]
    [InlineData(4, null, null, "int32")]
    [InlineData(4, "crc32", null, "int32 (crc32)")]
    [InlineData(1, null, "RGB", "int8 (enum)")]
    public void TypeLabel_Integer(int size, string? checksum, string? enumLabel, string expected)
    {
        var node = new DecodedInteger { Name = "v", Offset = 0, Size = size, Value = 0, ChecksumAlgorithm = checksum, EnumLabel = enumLabel };
        NodeDisplayText.TypeLabel(node).Should().Be(expected);
    }

    [Fact]
    public void TypeLabel_String_UsesEncodingAndSize()
    {
        var node = new DecodedString { Name = "tag", Offset = 0, Size = 4, Value = "IHDR", Encoding = "ASCII" };
        NodeDisplayText.TypeLabel(node).Should().Be("ascii[4]");
    }

    [Fact]
    public void TypeLabel_Bytes_WithChecksum()
    {
        var node = new DecodedBytes { Name = "crc", Offset = 0, Size = 4, RawBytes = new byte[4], ChecksumAlgorithm = "crc32" };
        NodeDisplayText.TypeLabel(node).Should().Be("bytes[4] (crc32)");
    }

    [Fact]
    public void TypeLabel_Compressed_ShowsAlgorithm()
    {
        var node = new DecodedCompressed { Name = "d", Offset = 0, Size = 10, Algorithm = "zlib", CompressedSize = 10, DecompressedSize = 20 };
        NodeDisplayText.TypeLabel(node).Should().Be("bytes (zlib)");
    }
}
