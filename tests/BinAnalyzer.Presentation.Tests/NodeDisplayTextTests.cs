using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
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
    public void TypeLabel_Integer_WithoutDslType_FallsBackToSize(int size, string? checksum, string? enumLabel, string expected)
    {
        var node = new DecodedInteger { Name = "v", Offset = 0, Size = size, Value = 0, ChecksumAlgorithm = checksum, EnumLabel = enumLabel };
        NodeDisplayText.TypeLabel(node).Should().Be(expected);
    }

    [Theory]
    [InlineData(FieldType.UInt32, 4, null, null, "u32")]
    [InlineData(FieldType.Int16, 2, null, null, "i16")]
    [InlineData(FieldType.UInt64, 8, null, null, "u64")]
    [InlineData(FieldType.UInt32, 4, "crc32", null, "u32 (crc32)")]
    [InlineData(FieldType.UInt8, 1, null, "RGB", "u8 (enum)")]
    [InlineData(FieldType.ULeb128, 3, null, null, "uleb128")]
    public void TypeLabel_Integer_WithDslType_IsExact(FieldType type, int size, string? checksum, string? enumLabel, string expected)
    {
        var node = new DecodedInteger { Name = "v", Offset = 0, Size = size, Value = 0, ChecksumAlgorithm = checksum, EnumLabel = enumLabel, DslType = type };
        NodeDisplayText.TypeLabel(node).Should().Be(expected);
        node.TypeName.Should().Be(FieldTypeNames.ToDslName(type));
    }

    [Fact]
    public void TypeLabel_EnumRefWithoutLabel_StillMarksEnum()
    {
        var node = new DecodedInteger { Name = "v", Offset = 0, Size = 1, Value = 99, DslType = FieldType.UInt8, EnumRef = "color" };
        NodeDisplayText.TypeLabel(node).Should().Be("u8 (enum)");
    }

    [Fact]
    public void TypeLabel_Float_String_Flags_Array_UseDslType()
    {
        NodeDisplayText.TypeLabel(new DecodedFloat { Name = "f", Offset = 0, Size = 4, Value = 0, Precision = FloatPrecision.Single, DslType = FieldType.Float32 }).Should().Be("f32");
        NodeDisplayText.TypeLabel(new DecodedFloat { Name = "f", Offset = 0, Size = 8, Value = 0, Precision = FloatPrecision.Double }).Should().Be("float64");
        NodeDisplayText.TypeLabel(new DecodedFloat { Name = "f", Offset = 0, Size = 2, Value = 0, Precision = FloatPrecision.Half, DslType = FieldType.Float16 }).Should().Be("f16");
        NodeDisplayText.TypeLabel(new DecodedFloat { Name = "f", Offset = 0, Size = 2, Value = 0, Precision = FloatPrecision.Half }).Should().Be("float16");
        NodeDisplayText.TypeLabel(new DecodedString { Name = "s", Offset = 0, Size = 6, Value = "ab", Encoding = "utf16le", DslType = FieldType.Utf16Le }).Should().Be("utf16le[6]");
        NodeDisplayText.TypeLabel(new DecodedString { Name = "s", Offset = 0, Size = 5, Value = "abcd", Encoding = "asciiz", DslType = FieldType.AsciiZ }).Should().Be("asciiz[5]");
        NodeDisplayText.TypeLabel(new DecodedFlags { Name = "fl", Offset = 0, Size = 4, RawValue = 1, FlagStates = [], DslType = FieldType.UInt32 }).Should().Be("u32 (flags)");
        NodeDisplayText.TypeLabel(new DecodedFlags { Name = "fl", Offset = 0, Size = 4, RawValue = 1, FlagStates = [] }).Should().Be("flags32");
        NodeDisplayText.TypeLabel(new DecodedArray { Name = "a", Offset = 0, Size = 0, Elements = [], DslType = FieldType.Struct }).Should().Be("struct[0]");
        NodeDisplayText.TypeLabel(new DecodedArray { Name = "a", Offset = 0, Size = 0, Elements = [] }).Should().Be("array[0]");
        NodeDisplayText.TypeLabel(new DecodedInteger { Name = "b", Offset = 0, Size = 3, BitOffset = 2, Value = 1, DslType = FieldType.UInt8 }).Should().Be("u8:3bit");
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

    [Fact]
    public void Virtual_WithEnum_IncludesLabel()
    {
        var node = new DecodedVirtual { Name = "os", Offset = 0, Size = 0, Value = 3L, EnumLabel = "unix" };

        NodeDisplayText.For(node).Should().Be("os: = 3 \"unix\"");
        NodeDisplayText.ValueOnly(node).Should().Be("= 3 \"unix\"");
    }

    [Fact]
    public void Virtual_WithoutEnum_Unchanged()
    {
        var node = new DecodedVirtual { Name = "n", Offset = 0, Size = 0, Value = 7L };

        NodeDisplayText.For(node).Should().Be("n: = 7");
    }
}
