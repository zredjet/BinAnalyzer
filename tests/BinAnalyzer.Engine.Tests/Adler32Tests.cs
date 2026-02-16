using System.Text;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class Adler32Tests
{
    [Fact]
    public void Compute_KnownVector_Wikipedia()
    {
        // "Wikipedia" の Adler-32 = 0x11E60398
        var data = Encoding.ASCII.GetBytes("Wikipedia");
        var result = Adler32Calculator.Compute(data);
        result.Should().Be(0x11E60398);
    }

    [Fact]
    public void Compute_EmptyData_ReturnsOne()
    {
        // Adler-32 の初期値は a=1, b=0 → (0 << 16) | 1 = 1
        var result = Adler32Calculator.Compute(ReadOnlySpan<byte>.Empty);
        result.Should().Be(1u);
    }

    [Fact]
    public void Compute_SingleByteA()
    {
        // 'a' (0x61): a = (1+97) % 65521 = 98, b = (0+98) % 65521 = 98
        // result = (98 << 16) | 98 = 0x00620062
        var result = Adler32Calculator.Compute(Encoding.ASCII.GetBytes("a"));
        result.Should().Be(0x00620062u);
    }

    [Fact]
    public void Compute_DifferentData_ReturnsDifferent()
    {
        var result1 = Adler32Calculator.Compute(Encoding.ASCII.GetBytes("hello"));
        var result2 = Adler32Calculator.Compute(Encoding.ASCII.GetBytes("world"));
        result1.Should().NotBe(result2);
    }
}
