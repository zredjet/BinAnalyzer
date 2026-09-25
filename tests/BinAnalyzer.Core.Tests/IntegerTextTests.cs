using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests;

/// <summary>REQ-201: 符号なし 64 ビットの値の表記と、DSL の整数のリテラル。</summary>
public class IntegerTextTests
{
    [Theory]
    [InlineData(-1L, FieldType.UInt64, "18446744073709551615")]
    [InlineData(long.MinValue, FieldType.UInt64, "9223372036854775808")]
    [InlineData(-1L, FieldType.ULeb128, "18446744073709551615")]
    [InlineData(-1L, FieldType.Vlq, "18446744073709551615")]
    [InlineData(42L, FieldType.UInt64, "42")]
    [InlineData(-1L, FieldType.Int64, "-1")]
    [InlineData(-1L, FieldType.SLeb128, "-1")]
    [InlineData(-1L, null, "-1")]
    public void Format_UsesUnsignedOnlyForUnsigned64BitTypes(long value, FieldType? type, string expected)
    {
        IntegerText.Format(value, type).Should().Be(expected);
    }

    [Theory]
    [InlineData(-1L, FieldType.UInt64, true)]
    [InlineData(15L, FieldType.UInt64, false)]
    [InlineData(-5L, FieldType.Int64, false)]
    [InlineData(-16L, FieldType.Int64, true)]
    public void ShowsHex_ComparesUnsignedForUnsigned64BitTypes(long value, FieldType type, bool expected)
    {
        IntegerText.ShowsHex(value, type).Should().Be(expected);
    }

    [Fact]
    public void FormatBits_IsAlwaysUnsigned()
    {
        IntegerText.FormatBits(long.MinValue).Should().Be("9223372036854775808");
        IntegerText.FormatBits(5).Should().Be("5");
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("42", 42L)]
    [InlineData("-5", -5L)]
    [InlineData("+7", 7L)]
    [InlineData("0x10", 16L)]
    [InlineData("0XFF", 255L)]
    [InlineData("1_000", 1000L)]
    [InlineData("-9223372036854775808", long.MinValue)]
    [InlineData("9223372036854775807", long.MaxValue)]
    [InlineData("9223372036854775808", long.MinValue)]         // 2^63 は同じビットの負の long
    [InlineData("18446744073709551615", -1L)]                  // 2^64 - 1
    [InlineData("0xFFFFFFFFFFFFFFFF", -1L)]
    [InlineData("0x8000000000000000", long.MinValue)]
    public void TryParseLiteral_ReadsDecimalAndHexUpTo2To64(string text, long expected)
    {
        IntegerText.TryParseLiteral(text, out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0x")]
    [InlineData("0xZZ")]
    [InlineData("18446744073709551616")]                        // 2^64
    [InlineData("0x10000000000000000")]                         // 17 桁
    [InlineData("-9223372036854775809")]                        // -2^63 - 1
    [InlineData("1.5")]
    public void TryParseLiteral_RejectsOutOfRangeAndNonIntegers(string text)
    {
        IntegerText.TryParseLiteral(text, out _).Should().BeFalse();
    }
}
