using System.Text;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class Crc16Tests
{
    [Fact]
    public void ComputeCcitt_KnownVector_ReturnsCorrect()
    {
        // "123456789" の CRC-16/CCITT = 0x29B1
        var data = Encoding.ASCII.GetBytes("123456789");
        var result = Crc16Calculator.ComputeCcitt(data);
        result.Should().Be(0x29B1);
    }

    [Fact]
    public void ComputeIbm_KnownVector_ReturnsCorrect()
    {
        // "123456789" の CRC-16/IBM = 0xBB3D
        var data = Encoding.ASCII.GetBytes("123456789");
        var result = Crc16Calculator.ComputeIbm(data);
        result.Should().Be(0xBB3D);
    }

    [Fact]
    public void ComputeCcitt_EmptyData_ReturnsInitialValue()
    {
        var result = Crc16Calculator.ComputeCcitt(ReadOnlySpan<byte>.Empty);
        result.Should().Be(0xFFFF);
    }

    [Fact]
    public void ComputeIbm_EmptyData_ReturnsZero()
    {
        var result = Crc16Calculator.ComputeIbm(ReadOnlySpan<byte>.Empty);
        result.Should().Be(0x0000);
    }

    [Fact]
    public void ComputeCcitt_DifferentData_ReturnsDifferentCrc()
    {
        var crc1 = Crc16Calculator.ComputeCcitt(Encoding.ASCII.GetBytes("IHDR"));
        var crc2 = Crc16Calculator.ComputeCcitt(Encoding.ASCII.GetBytes("IEND"));
        crc1.Should().NotBe(crc2);
    }

    [Fact]
    public void ComputeIbm_DifferentData_ReturnsDifferentCrc()
    {
        var crc1 = Crc16Calculator.ComputeIbm(Encoding.ASCII.GetBytes("IHDR"));
        var crc2 = Crc16Calculator.ComputeIbm(Encoding.ASCII.GetBytes("IEND"));
        crc1.Should().NotBe(crc2);
    }
}
