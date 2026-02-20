using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Tui;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Tui.Tests;

public sealed class NodeDetailFormatterTests
{
    [Fact]
    public void Format_Integer_ReturnsBasicDetails()
    {
        var node = new DecodedInteger
        {
            Name = "width",
            Offset = 0x10,
            Size = 4,
            Value = 800,
        };

        var details = NodeDetailFormatter.Format(node);

        details.Should().ContainEquivalentOf(("Name", "width"));
        details.Should().ContainEquivalentOf(("Type", "integer"));
        details.Should().ContainEquivalentOf(("Value", "800"));
        details.Should().ContainEquivalentOf(("Offset", "0x00000010 (16)"));
        details.Should().ContainEquivalentOf(("Size", "4 bytes"));
        details.Should().ContainEquivalentOf(("Hex", "0x320"));
    }

    [Fact]
    public void Format_Integer_SmallValue_NoHexEntry()
    {
        var node = new DecodedInteger
        {
            Name = "flags",
            Offset = 0,
            Size = 1,
            Value = 8,
        };

        var details = NodeDetailFormatter.Format(node);

        details.Should().NotContain(d => d.Key == "Hex");
    }

    [Fact]
    public void Format_Integer_WithEnum_IncludesEnumLabel()
    {
        var node = new DecodedInteger
        {
            Name = "color_type",
            Offset = 0,
            Size = 1,
            Value = 6,
            EnumLabel = "RGBA",
            EnumDescription = "Truecolor with alpha",
        };

        var details = NodeDetailFormatter.Format(node);

        details.Should().ContainEquivalentOf(("Enum", "RGBA"));
        details.Should().ContainEquivalentOf(("Description", "Truecolor with alpha"));
    }

    [Fact]
    public void Format_String_ReturnsValueAndEncoding()
    {
        var node = new DecodedString
        {
            Name = "signature",
            Offset = 0,
            Size = 4,
            Value = "PNG",
            Encoding = "ASCII",
        };

        var details = NodeDetailFormatter.Format(node);

        details.Should().ContainEquivalentOf(("Type", "string"));
        details.Should().ContainEquivalentOf(("Value", "PNG"));
        details.Should().ContainEquivalentOf(("Encoding", "ASCII"));
    }

    [Fact]
    public void Format_Struct_ReturnsChildCount()
    {
        var node = new DecodedStruct
        {
            Name = "ihdr",
            StructType = "IHDR",
            Offset = 0,
            Size = 13,
            Children =
            [
                new DecodedInteger { Name = "width", Offset = 0, Size = 4, Value = 800 },
                new DecodedInteger { Name = "height", Offset = 4, Size = 4, Value = 600 },
            ],
        };

        var details = NodeDetailFormatter.Format(node);

        details.Should().ContainEquivalentOf(("Type", "struct"));
        details.Should().ContainEquivalentOf(("StructType", "IHDR"));
        details.Should().ContainEquivalentOf(("Children", "2 fields"));
    }

    [Fact]
    public void Format_Array_ReturnsElementCount()
    {
        var node = new DecodedArray
        {
            Name = "chunks",
            Offset = 0,
            Size = 100,
            Elements =
            [
                new DecodedStruct { Name = "chunk0", StructType = "Chunk", Offset = 0, Size = 50, Children = [] },
                new DecodedStruct { Name = "chunk1", StructType = "Chunk", Offset = 50, Size = 50, Children = [] },
            ],
        };

        var details = NodeDetailFormatter.Format(node);

        details.Should().ContainEquivalentOf(("Type", "array"));
        details.Should().ContainEquivalentOf(("Elements", "2 items"));
    }

    [Fact]
    public void Format_Bytes_ReturnsHexPreview()
    {
        var node = new DecodedBytes
        {
            Name = "magic",
            Offset = 0,
            Size = 4,
            RawBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 },
        };

        var details = NodeDetailFormatter.Format(node);

        details.Should().ContainEquivalentOf(("Type", "bytes"));
        details.Should().ContainEquivalentOf(("Hex", "89 50 4E 47"));
    }

    [Fact]
    public void Format_Float_ReturnsValue()
    {
        var node = new DecodedFloat
        {
            Name = "ratio",
            Offset = 0,
            Size = 4,
            Value = 3.14,
            IsSinglePrecision = true,
        };

        var details = NodeDetailFormatter.Format(node);

        details.Should().ContainEquivalentOf(("Type", "float32"));
        details.Should().Contain(d => d.Key == "Value" && d.Value.StartsWith("3.14"));
    }

    [Fact]
    public void Format_Float64_ReturnsFloat64Type()
    {
        var node = new DecodedFloat
        {
            Name = "ratio",
            Offset = 0,
            Size = 8,
            Value = 3.14,
            IsSinglePrecision = false,
        };

        var details = NodeDetailFormatter.Format(node);

        details.Should().ContainEquivalentOf(("Type", "float64"));
    }

    [Fact]
    public void Format_Compressed_ReturnsAlgorithmAndSizes()
    {
        var node = new DecodedCompressed
        {
            Name = "data",
            Offset = 0,
            Size = 100,
            Algorithm = "zlib",
            CompressedSize = 100,
            DecompressedSize = 500,
        };

        var details = NodeDetailFormatter.Format(node);

        details.Should().ContainEquivalentOf(("Type", "compressed"));
        details.Should().ContainEquivalentOf(("Algorithm", "zlib"));
        details.Should().ContainEquivalentOf(("Compressed", "100 bytes"));
        details.Should().ContainEquivalentOf(("Decompressed", "500 bytes"));
    }

    [Fact]
    public void Format_Error_ReturnsErrorMessage()
    {
        var node = new DecodedError
        {
            Name = "bad_field",
            Offset = 0,
            Size = 0,
            ErrorMessage = "unexpected EOF",
        };

        var details = NodeDetailFormatter.Format(node);

        details.Should().ContainEquivalentOf(("Type", "error"));
        details.Should().ContainEquivalentOf(("Error", "unexpected EOF"));
    }

    [Fact]
    public void Format_WithValidation_IncludesValidationResult()
    {
        var node = new DecodedInteger
        {
            Name = "version",
            Offset = 0,
            Size = 1,
            Value = 1,
            Validation = new ValidationInfo(true, "value == 1"),
        };

        var details = NodeDetailFormatter.Format(node);

        details.Should().Contain(d => d.Key == "Validation" && d.Value.Contains("PASS"));
    }

    [Fact]
    public void Format_Virtual_ReturnsValue()
    {
        var node = new DecodedVirtual
        {
            Name = "computed",
            Offset = 0,
            Size = 0,
            Value = 42,
        };

        var details = NodeDetailFormatter.Format(node);

        details.Should().ContainEquivalentOf(("Type", "virtual"));
        details.Should().ContainEquivalentOf(("Value", "42"));
    }
}
