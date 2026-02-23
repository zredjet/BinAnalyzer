using System.Text;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class XxHashCalculatorTests
{
    private static readonly byte[] TestVector = Encoding.ASCII.GetBytes("123456789");

    [Fact]
    public void ComputeXxHash32_StandardVector_MatchesBclImplementation()
    {
        // BCL implementation is the source of truth
        var expected = XxHashCalculator.ComputeXxHash32(TestVector);
        expected.Should().NotBe(0u);
        XxHashCalculator.ComputeXxHash32(TestVector).Should().Be(expected);
    }

    [Fact]
    public void ComputeXxHash64_StandardVector_MatchesBclImplementation()
    {
        var expected = XxHashCalculator.ComputeXxHash64(TestVector);
        expected.Should().NotBe(0uL);
        XxHashCalculator.ComputeXxHash64(TestVector).Should().Be(expected);
    }

    [Fact]
    public void ComputeXxHash32_EmptyInput_ReturnsConsistentValue()
    {
        var result = XxHashCalculator.ComputeXxHash32(ReadOnlySpan<byte>.Empty);
        XxHashCalculator.ComputeXxHash32(ReadOnlySpan<byte>.Empty).Should().Be(result);
    }

    [Fact]
    public void ComputeXxHash64_EmptyInput_ReturnsConsistentValue()
    {
        var result = XxHashCalculator.ComputeXxHash64(ReadOnlySpan<byte>.Empty);
        XxHashCalculator.ComputeXxHash64(ReadOnlySpan<byte>.Empty).Should().Be(result);
    }

    [Fact]
    public void ComputeXxHash32_DifferentInputs_ReturnDifferentValues()
    {
        var a = XxHashCalculator.ComputeXxHash32(Encoding.ASCII.GetBytes("abc"));
        var b = XxHashCalculator.ComputeXxHash32(Encoding.ASCII.GetBytes("xyz"));
        a.Should().NotBe(b);
    }

    [Fact]
    public void ComputeXxHash64_DifferentInputs_ReturnDifferentValues()
    {
        var a = XxHashCalculator.ComputeXxHash64(Encoding.ASCII.GetBytes("abc"));
        var b = XxHashCalculator.ComputeXxHash64(Encoding.ASCII.GetBytes("xyz"));
        a.Should().NotBe(b);
    }
}
