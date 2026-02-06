using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class MapOutputTests
{
    [Fact]
    public void MapOutput_ContainsHeader()
    {
        var (root, data) = CreateSampleData();
        var formatter = new MapOutputFormatter();

        var output = formatter.Format(root, data);

        output.Should().Contain("BinAnalyzer - Binary Map");
        output.Should().Contain("30 bytes");
    }

    [Fact]
    public void MapOutput_ContainsFieldPaths()
    {
        var (root, data) = CreateSampleData();
        var formatter = new MapOutputFormatter();

        var output = formatter.Format(root, data);

        output.Should().Contain("magic");
        output.Should().Contain("type");
        output.Should().Contain("header.width");
        output.Should().Contain("header.height");
        output.Should().Contain("data");
    }

    [Fact]
    public void MapOutput_ContainsOffsets()
    {
        var (root, data) = CreateSampleData();
        var formatter = new MapOutputFormatter();

        var output = formatter.Format(root, data);

        output.Should().Contain("0x00000000");
        output.Should().Contain("0x00000004");
    }

    [Fact]
    public void MapOutput_ContainsBars()
    {
        var (root, data) = CreateSampleData();
        var formatter = new MapOutputFormatter();

        var output = formatter.Format(root, data);

        // Alternating bar characters
        output.Should().Contain("█");
        output.Should().Contain("░");
        output.Should().Contain("┃");
    }

    [Fact]
    public void MapOutput_ContainsLegend()
    {
        var (root, data) = CreateSampleData();
        var formatter = new MapOutputFormatter();

        var output = formatter.Format(root, data);

        output.Should().Contain("Legend:");
        output.Should().Contain("1 char = 1 byte");
    }

    [Fact]
    public void MapOutput_ColorAlways_ContainsAnsiCodes()
    {
        var (root, data) = CreateSampleData();
        var formatter = new MapOutputFormatter(ColorMode.Always);

        var output = formatter.Format(root, data);

        output.Should().Contain("\x1b[");
    }

    [Fact]
    public void MapOutput_ColorNever_NoAnsiCodes()
    {
        var (root, data) = CreateSampleData();
        var formatter = new MapOutputFormatter(ColorMode.Never);

        var output = formatter.Format(root, data);

        output.Should().NotContain("\x1b[");
    }

    [Fact]
    public void MapOutput_LargeFile_ScalesDown()
    {
        var root = new DecodedStruct
        {
            Name = "big", StructType = "big", Offset = 0, Size = 10000,
            Children =
            [
                new DecodedInteger { Name = "a", Offset = 0, Size = 5000, Value = 0 },
                new DecodedInteger { Name = "b", Offset = 5000, Size = 5000, Value = 0 },
            ],
        };
        var data = new byte[10000];
        var formatter = new MapOutputFormatter();

        var output = formatter.Format(root, data);

        // Scale legend should show approximate bytes per char
        output.Should().Contain("1 char ≈");
        output.Should().Contain("bytes");
    }

    [Fact]
    public void MapOutput_ArrayElements_IndexedPaths()
    {
        var root = new DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 12,
            Children =
            [
                new DecodedArray
                {
                    Name = "items", Offset = 0, Size = 12,
                    Elements =
                    [
                        new DecodedInteger { Name = "item", Offset = 0, Size = 4, Value = 1 },
                        new DecodedInteger { Name = "item", Offset = 4, Size = 4, Value = 2 },
                        new DecodedInteger { Name = "item", Offset = 8, Size = 4, Value = 3 },
                    ],
                },
            ],
        };
        var data = new byte[12];
        var formatter = new MapOutputFormatter();

        var output = formatter.Format(root, data);

        output.Should().Contain("items[0]");
        output.Should().Contain("items[1]");
        output.Should().Contain("items[2]");
    }

    private static (DecodedStruct root, byte[] data) CreateSampleData()
    {
        var root = new DecodedStruct
        {
            Name = "test", StructType = "test", Offset = 0, Size = 30,
            Children =
            [
                new DecodedInteger { Name = "magic", Offset = 0, Size = 4, Value = 0x89504E47 },
                new DecodedString { Name = "type", Offset = 4, Size = 4, Value = "IHDR", Encoding = "ascii" },
                new DecodedStruct
                {
                    Name = "header", StructType = "header", Offset = 8, Size = 13,
                    Children =
                    [
                        new DecodedInteger { Name = "width", Offset = 8, Size = 4, Value = 1920 },
                        new DecodedInteger { Name = "height", Offset = 12, Size = 4, Value = 1080 },
                        new DecodedInteger { Name = "bit_depth", Offset = 16, Size = 1, Value = 8 },
                    ],
                },
                new DecodedBytes { Name = "data", Offset = 21, Size = 9, RawBytes = new byte[9] },
            ],
        };
        return (root, new byte[30]);
    }
}
