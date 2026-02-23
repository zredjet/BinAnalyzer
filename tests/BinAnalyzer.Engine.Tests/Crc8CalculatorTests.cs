using System.Text;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class Crc8CalculatorTests
{
    private static readonly byte[] TestVector = Encoding.ASCII.GetBytes("123456789");

    [Fact]
    public void ComputeSmbus_StandardVector_ReturnsExpected()
    {
        Crc8Calculator.ComputeSmbus(TestVector).Should().Be(0xF4);
    }

    [Fact]
    public void ComputeMaxim_StandardVector_ReturnsExpected()
    {
        Crc8Calculator.ComputeMaxim(TestVector).Should().Be(0xA1);
    }

    [Fact]
    public void ComputeCdma2000_StandardVector_ReturnsExpected()
    {
        Crc8Calculator.ComputeCdma2000(TestVector).Should().Be(0xDA);
    }

    [Fact]
    public void ComputeSmbus_EmptyInput_ReturnsZero()
    {
        Crc8Calculator.ComputeSmbus(ReadOnlySpan<byte>.Empty).Should().Be(0x00);
    }

    [Fact]
    public void ComputeMaxim_EmptyInput_ReturnsZero()
    {
        Crc8Calculator.ComputeMaxim(ReadOnlySpan<byte>.Empty).Should().Be(0x00);
    }

    [Fact]
    public void ComputeCdma2000_EmptyInput_ReturnsFF()
    {
        // CDMA2000 has init=0xFF
        Crc8Calculator.ComputeCdma2000(ReadOnlySpan<byte>.Empty).Should().Be(0xFF);
    }

    [Fact]
    public void ComputeSmbus_SingleByte_IsConsistent()
    {
        var data = new byte[] { 0x00 };
        var result = Crc8Calculator.ComputeSmbus(data);
        // Should be deterministic
        Crc8Calculator.ComputeSmbus(data).Should().Be(result);
    }
}
