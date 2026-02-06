using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class PaddingOutputTests
{
    [Fact]
    public void PaddingField_HiddenInTreeOutput()
    {
        var root = new DecodedStruct
        {
            Name = "test", StructType = "test", Offset = 0, Size = 4,
            Children =
            [
                new DecodedInteger { Name = "value", Offset = 0, Size = 1, Value = 1 },
                new DecodedBytes
                {
                    Name = "pad", Offset = 1, Size = 3,
                    RawBytes = new byte[] { 0, 0, 0 },
                    IsPadding = true,
                },
            ],
        };

        var formatter = new TreeOutputFormatter();
        var output = formatter.Format(root);

        output.Should().Contain("value");
        output.Should().NotContain("pad");
    }

    [Fact]
    public void NonPaddingField_NotHiddenInTreeOutput()
    {
        var root = new DecodedStruct
        {
            Name = "test", StructType = "test", Offset = 0, Size = 4,
            Children =
            [
                new DecodedInteger { Name = "value", Offset = 0, Size = 1, Value = 1 },
                new DecodedBytes
                {
                    Name = "data", Offset = 1, Size = 3,
                    RawBytes = new byte[] { 0, 0, 0 },
                    IsPadding = false,
                },
            ],
        };

        var formatter = new TreeOutputFormatter();
        var output = formatter.Format(root);

        output.Should().Contain("value");
        output.Should().Contain("data");
    }

    [Fact]
    public void PaddingField_NotHiddenInJsonOutput()
    {
        var root = new DecodedStruct
        {
            Name = "test", StructType = "test", Offset = 0, Size = 4,
            Children =
            [
                new DecodedInteger { Name = "value", Offset = 0, Size = 1, Value = 1 },
                new DecodedBytes
                {
                    Name = "pad", Offset = 1, Size = 3,
                    RawBytes = new byte[] { 0, 0, 0 },
                    IsPadding = true,
                },
            ],
        };

        var formatter = new JsonOutputFormatter();
        var output = formatter.Format(root);

        // JSON output should include padding fields (they are still in the data)
        output.Should().Contain("value");
        output.Should().Contain("pad");
    }
}
