using System.Buffers.Binary;
using System.Text;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>REQ-199: Ogg のページの CRC-32（多項式 0x04C11DB7・非反転・初期値 0・最後の XOR 無し）。</summary>
public class Crc32OggCalculatorTests
{
    [Fact]
    public void CheckValue()
    {
        Crc32Calculator.ComputeOgg(Encoding.ASCII.GetBytes("123456789")).Should().Be(0x89A1897F);
        Crc32Calculator.ComputeOgg([]).Should().Be(0u);
    }

    [Fact]
    public void DiffersFromTheZipCrc32()
    {
        var data = Encoding.ASCII.GetBytes("123456789");
        Crc32Calculator.ComputeOgg(data).Should().NotBe(Crc32Calculator.Compute(data));
    }

    [Fact]
    public void MatchesAPageWrittenByFfmpeg()
    {
        // ffmpeg 9.0 の Ogg の muxer が書いた Opus の最初のページ（OpusHead、47 バイト）。CRC はオフセット 22 のリトルエンディアンの 0x0F40BFE5
        var page = Convert.FromHexString(
            "4F67675300020000000000000000EED246BF00000000E5BF400F01134F707573486561640101380180BB0000000000");
        var stored = BinaryPrimitives.ReadUInt32LittleEndian(page.AsSpan(22));
        page.AsSpan(22, 4).Clear();

        stored.Should().Be(0x0F40BFE5);
        Crc32Calculator.ComputeOgg(page).Should().Be(stored);
    }
}
