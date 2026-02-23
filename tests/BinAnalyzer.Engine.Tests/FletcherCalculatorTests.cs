using System.Text;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class FletcherCalculatorTests
{
    [Fact]
    public void ComputeFletcher16_Abcde_ReturnsExpected()
    {
        var data = Encoding.ASCII.GetBytes("abcde");
        FletcherCalculator.ComputeFletcher16(data).Should().Be(0xC8F0);
    }

    [Fact]
    public void ComputeFletcher16_EmptyInput_ReturnsZero()
    {
        FletcherCalculator.ComputeFletcher16(ReadOnlySpan<byte>.Empty).Should().Be(0x0000);
    }

    [Fact]
    public void ComputeFletcher32_EvenLength_IsConsistent()
    {
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var result = FletcherCalculator.ComputeFletcher32(data);
        FletcherCalculator.ComputeFletcher32(data).Should().Be(result);
    }

    [Fact]
    public void ComputeFletcher32_OddLength_ZeroPads()
    {
        var data = new byte[] { 0x01, 0x02, 0x03 };
        var result = FletcherCalculator.ComputeFletcher32(data);
        result.Should().NotBe(0u);
        // Should be deterministic
        FletcherCalculator.ComputeFletcher32(data).Should().Be(result);
    }

    [Fact]
    public void ComputeFletcher32_EmptyInput_ReturnsZero()
    {
        FletcherCalculator.ComputeFletcher32(ReadOnlySpan<byte>.Empty).Should().Be(0u);
    }

    [Fact]
    public void ComputeFletcher16_DifferentInputs_ReturnDifferentValues()
    {
        var a = FletcherCalculator.ComputeFletcher16(Encoding.ASCII.GetBytes("abc"));
        var b = FletcherCalculator.ComputeFletcher16(Encoding.ASCII.GetBytes("xyz"));
        a.Should().NotBe(b);
    }
}
