using System.Text;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class Crc32Tests
{
    [Fact]
    public void Compute_KnownVector_ReturnsCorrect()
    {
        // "123456789" の CRC-32 = 0xCBF43926 (ISO 3309標準テストベクター)
        var data = Encoding.ASCII.GetBytes("123456789");
        var result = Crc32Calculator.Compute(data);
        result.Should().Be(0xCBF43926);
    }

    [Fact]
    public void Compute_EmptyData_ReturnsZero()
    {
        var result = Crc32Calculator.Compute(ReadOnlySpan<byte>.Empty);
        result.Should().Be(0x00000000);
    }

    [Fact]
    public void Compute_SingleByte_ReturnsCorrect()
    {
        // CRC-32 of byte 0x00 = 0xD202EF8D
        var result = Crc32Calculator.Compute(new byte[] { 0x00 });
        result.Should().Be(0xD202EF8D);
    }

    [Fact]
    public void Compute_ConsecutiveBytes_IsConsistent()
    {
        // 同じデータに対して複数回計算しても同じ結果になること
        var data = Encoding.ASCII.GetBytes("IHDR");
        var crc1 = Crc32Calculator.Compute(data);
        var crc2 = Crc32Calculator.Compute(data);
        crc1.Should().Be(crc2);
    }

    [Fact]
    public void Compute_DifferentData_ReturnsDifferentCrc()
    {
        var crc1 = Crc32Calculator.Compute(Encoding.ASCII.GetBytes("IHDR"));
        var crc2 = Crc32Calculator.Compute(Encoding.ASCII.GetBytes("IEND"));
        crc1.Should().NotBe(crc2);
    }
}
