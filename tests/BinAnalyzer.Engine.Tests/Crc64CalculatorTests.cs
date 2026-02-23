using System.Text;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class Crc64CalculatorTests
{
    private static readonly byte[] TestVector = Encoding.ASCII.GetBytes("123456789");

    [Fact]
    public void ComputeEcma_StandardVector_ReturnsExpected()
    {
        Crc64Calculator.ComputeEcma(TestVector).Should().Be(0x6C40DF5F0B497347uL);
    }

    [Fact]
    public void ComputeEcma_EmptyInput_ReturnsZero()
    {
        Crc64Calculator.ComputeEcma(ReadOnlySpan<byte>.Empty).Should().Be(0x0000000000000000uL);
    }

    [Fact]
    public void ComputeEcma_SingleByte_IsConsistent()
    {
        var data = new byte[] { 0x42 };
        var result = Crc64Calculator.ComputeEcma(data);
        Crc64Calculator.ComputeEcma(data).Should().Be(result);
    }
}
