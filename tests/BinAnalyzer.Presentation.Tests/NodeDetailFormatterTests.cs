using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Presentation;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Presentation.Tests;

public sealed class NodeDetailFormatterTests
{
    [Fact]
    public void Format_Offset_IsMonoRow()
    {
        var node = new DecodedInteger { Name = "x", Offset = 1, Size = 1, Value = 1 };
        NodeDetailFormatter.Format(node).Should().Contain(d => d.Key == "Offset" && d.Kind == DetailRowKind.Mono);
    }

    [Fact]
    public void Format_Flags_ChildRowsAreMarkedChild()
    {
        var node = new DecodedFlags
        {
            Name = "f", Offset = 0, Size = 1, RawValue = 1,
            FlagStates = [new FlagState("a", true, 0, null), new FlagState("b", false, 1, "off")],
        };
        var rows = NodeDetailFormatter.Format(node);
        rows.Where(r => r.Kind == DetailRowKind.Child).Should().HaveCount(2);
        rows.Should().ContainRow("  a", "set");
        rows.Should().ContainRow("  b", "off");
    }

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

        details.Should().ContainRow("Name", "width");
        details.Should().ContainRow("Type", "integer");
        details.Should().ContainRow("Value", "800");
        details.Should().ContainRow("Offset", "0x00000010 (16)");
        details.Should().ContainRow("Size", "4 bytes");
        details.Should().ContainRow("Hex", "0x320");
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

        details.Should().ContainRow("Enum", "RGBA");
        details.Should().ContainRow("Description", "Truecolor with alpha");
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

        details.Should().ContainRow("Type", "string");
        details.Should().ContainRow("Value", "PNG");
        details.Should().ContainRow("Encoding", "ASCII");
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

        details.Should().ContainRow("Type", "struct");
        details.Should().ContainRow("StructType", "IHDR");
        details.Should().ContainRow("Children", "2 fields");
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

        details.Should().ContainRow("Type", "array");
        details.Should().ContainRow("Elements", "2 items");
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

        details.Should().ContainRow("Type", "bytes");
        details.Should().ContainRow("Hex", "89 50 4E 47");
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
            Precision = FloatPrecision.Single,
        };

        var details = NodeDetailFormatter.Format(node);

        details.Should().ContainRow("Type", "float32");
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
            Precision = FloatPrecision.Double,
        };

        var details = NodeDetailFormatter.Format(node);

        details.Should().ContainRow("Type", "float64");
    }

    [Fact]
    public void Format_Float16_ReturnsFloat16Type()
    {
        // REQ-200: DslType が無くても精度から型名を出す
        var node = new DecodedFloat { Name = "h", Offset = 0, Size = 2, Value = 65504, Precision = FloatPrecision.Half };

        var details = NodeDetailFormatter.Format(node);

        details.Should().ContainRow("Type", "float16");
        details.Should().ContainRow("Value", "65504");
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

        details.Should().ContainRow("Type", "compressed");
        details.Should().ContainRow("Algorithm", "zlib");
        details.Should().ContainRow("Compressed", "100 bytes");
        details.Should().ContainRow("Decompressed", "500 bytes");
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

        details.Should().ContainRow("Type", "error");
        details.Should().ContainRow("Error", "unexpected EOF");
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

        details.Should().ContainRow("Type", "virtual");
        details.Should().ContainRow("Value", "42");
    }

    [Fact]
    public void Format_VirtualWithEnum_IncludesEnumAndDescription()
    {
        var node = new DecodedVirtual
        {
            Name = "os",
            Offset = 0,
            Size = 0,
            Value = 3L,
            EnumLabel = "unix",
            EnumDescription = "UNIX 系",
        };

        var details = NodeDetailFormatter.Format(node);

        details.Should().ContainRow("Enum", "unix");
        details.Should().ContainRow("Description", "UNIX 系");
    }
}

internal static class DetailRowAssertions
{
    public static void ContainRow(this FluentAssertions.Collections.GenericCollectionAssertions<DetailRow> a, string key, string value)
        => a.Contain(d => d.Key == key && d.Value == value, $"expected row ({key}, {value})");
}
