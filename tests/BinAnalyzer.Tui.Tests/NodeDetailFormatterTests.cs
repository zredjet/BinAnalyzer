using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Tui.Tests;

public class NodeDetailFormatterTests
{
    [Theory]
    [InlineData(FloatPrecision.Half, 2, "float16")]     // REQ-200
    [InlineData(FloatPrecision.Single, 4, "float32")]
    [InlineData(FloatPrecision.Double, 8, "float64")]
    public void Float_ShowsThePrecisionAsTheType(FloatPrecision precision, int size, string expected)
    {
        var details = NodeDetailFormatter.Format(new DecodedFloat { Name = "f", Offset = 0, Size = size, Value = 1.5, Precision = precision });

        details.Should().Contain(("Type", expected));
    }

    [Fact]
    public void Uint64AboveInt64_ShowsTheUnsignedValue()
    {
        // REQ-201
        var details = NodeDetailFormatter.Format(new DecodedInteger { Name = "x", Offset = 0, Size = 8, Value = long.MinValue, DslType = FieldType.UInt64 });

        details.Should().Contain(("Value", "9223372036854775808")).And.Contain(("Hex", "0x8000000000000000"));
    }
}
