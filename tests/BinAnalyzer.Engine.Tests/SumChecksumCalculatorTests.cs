using System.Text;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>REQ-199: インターネットチェックサム（RFC 1071）と、ビッグエンディアンの uint32 の合計（OpenType の表のチェックサム）。</summary>
public class SumChecksumCalculatorTests
{
    [Fact]
    public void Internet_MatchesTheRfc1071Example()
    {
        // RFC 1071 の 3 節の例: 和 0xDDF2 の 1 の補数
        SumChecksumCalculator.ComputeInternet(Convert.FromHexString("0001F203F4F5F6F7")).Should().Be(0x220D);
    }

    [Fact]
    public void Internet_OfAnIpv4HeaderWithTheFieldZeroed_IsTheStoredChecksum()
    {
        // よく引かれる IPv4 のヘッダの例（192.168.0.1 → 192.168.0.199、UDP、チェックサム 0xB861）
        SumChecksumCalculator.ComputeInternet(Convert.FromHexString("450000730000400040110000C0A80001C0A800C7")).Should().Be(0xB861);
    }

    [Fact]
    public void Internet_OddLength_PadsWithAZeroByte()
    {
        SumChecksumCalculator.ComputeInternet([0x01, 0x02, 0x03])
            .Should().Be(SumChecksumCalculator.ComputeInternet([0x01, 0x02, 0x03, 0x00]))
            .And.Be(0xFBFD);   // ~(0x0102 + 0x0300)
    }

    [Fact]
    public void Internet_CarriesTheOverflowIntoTheLowBits()
    {
        // 0xFFFF + 0x0002 = 0x1_0001 → 桁あふれを足して 0x0002 → 1 の補数 0xFFFD
        SumChecksumCalculator.ComputeInternet([0xFF, 0xFF, 0x00, 0x02]).Should().Be(0xFFFD);
        SumChecksumCalculator.ComputeInternet([]).Should().Be(0xFFFF);
    }

    [Fact]
    public void Sum32_MatchesFontTools()
    {
        // fontTools 4.63 の sfnt.calcChecksum で求めた値
        SumChecksumCalculator.ComputeSum32BigEndian(Encoding.ASCII.GetBytes("123456789")).Should().Be(0x9F686A6C);
        SumChecksumCalculator.ComputeSum32BigEndian([1, 2, 3, 4, 5, 6, 7]).Should().Be(0x06080A04);
    }

    [Fact]
    public void Sum32_WrapsAround2To32()
    {
        SumChecksumCalculator.ComputeSum32BigEndian([0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x02]).Should().Be(1u);
    }

    [Fact]
    public void Sum32_LengthNotAMultipleOf4_PadsWithZeros()
    {
        SumChecksumCalculator.ComputeSum32BigEndian([0x12, 0x34, 0x56])
            .Should().Be(SumChecksumCalculator.ComputeSum32BigEndian([0x12, 0x34, 0x56, 0x00]))
            .And.Be(0x12345600u);
    }
}
