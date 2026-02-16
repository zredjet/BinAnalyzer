using System.Text;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class HashCalculatorTests
{
    [Fact]
    public void ComputeMd5_KnownVector()
    {
        // MD5("") = d41d8cd98f00b204e9800998ecf8427e
        var result = HashCalculator.ComputeMd5(ReadOnlySpan<byte>.Empty);
        Convert.ToHexString(result).Should().BeEquivalentTo("D41D8CD98F00B204E9800998ECF8427E");
    }

    [Fact]
    public void ComputeMd5_HelloWorld()
    {
        // MD5("Hello, World!") = 65a8e27d8879283831b664bd8b7f0ad4
        var data = Encoding.UTF8.GetBytes("Hello, World!");
        var result = HashCalculator.ComputeMd5(data);
        Convert.ToHexString(result).Should().BeEquivalentTo("65A8E27D8879283831B664BD8B7F0AD4");
    }

    [Fact]
    public void ComputeSha1_KnownVector()
    {
        // SHA-1("") = da39a3ee5e6b4b0d3255bfef95601890afd80709
        var result = HashCalculator.ComputeSha1(ReadOnlySpan<byte>.Empty);
        Convert.ToHexString(result).Should().BeEquivalentTo("DA39A3EE5E6B4B0D3255BFEF95601890AFD80709");
    }

    [Fact]
    public void ComputeSha1_HelloWorld()
    {
        // SHA-1("Hello, World!") = 0a0a9f2a6772942557ab5355d76af442f8f65e01
        var data = Encoding.UTF8.GetBytes("Hello, World!");
        var result = HashCalculator.ComputeSha1(data);
        Convert.ToHexString(result).Should().BeEquivalentTo("0A0A9F2A6772942557AB5355D76AF442F8F65E01");
    }

    [Fact]
    public void ComputeSha256_KnownVector()
    {
        // SHA-256("") = e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
        var result = HashCalculator.ComputeSha256(ReadOnlySpan<byte>.Empty);
        Convert.ToHexString(result).Should().BeEquivalentTo("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855");
    }

    [Fact]
    public void ComputeSha256_HelloWorld()
    {
        // SHA-256("Hello, World!") = dffd6021bb2bd5b0af676290809ec3a53191dd81c7f70a4b28688a362182986f
        var data = Encoding.UTF8.GetBytes("Hello, World!");
        var result = HashCalculator.ComputeSha256(data);
        Convert.ToHexString(result).Should().BeEquivalentTo("DFFD6021BB2BD5B0AF676290809EC3A53191DD81C7F70A4B28688A362182986F");
    }
}
