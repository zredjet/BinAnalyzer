using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

/// <summary>REQ-156: <c>diff --output hexdump</c> のバイト単位の差分。</summary>
public sealed class HexDiffOutputTests
{
    private static readonly string PngFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "png.bdef.yaml");

    private static (byte[] Left, byte[] Right) ModifiedPng()
    {
        var left = PngTestDataGenerator.CreateMinimalPng();
        var right = (byte[])left.Clone();
        right[0x13] ^= 0x03;   // IHDR width の最下位バイト
        return (left, right);
    }

    private static string[] Lines(string text) => text.Replace("\r\n", "\n").Split('\n');

    [Fact]
    public void DifferingRow_ShownAsMinusPlusPair_WithCaretsAndFieldPath()
    {
        var (left, right) = ModifiedPng();
        var format = new YamlFormatLoader().Load(PngFormatPath);
        var decoder = new BinaryDecoder();
        var formatter = new HexDiffOutputFormatter(ColorMode.Never);

        var output = formatter.Format(left, decoder.Decode(left, format), right, decoder.Decode(right, format), onlyDiff: false);

        formatter.HasDifferences.Should().BeTrue();
        var lines = Lines(output);
        var minus = Array.FindIndex(lines, l => l.StartsWith("- 00000010"));
        minus.Should().BeGreaterThan(0);
        lines[minus].Should().EndWith("chunks[0].data.width");
        lines[minus + 1].Should().StartWith("+ 00000010");
        lines[minus + 2].Should().Be("                     ^^", "13 バイト目（行内 3 列目）の下に ^^");
        lines.Should().Contain(l => l.StartsWith("  00000000"), "同じ行は 1 行で出す");
        output.Should().Contain("差分: 1 バイト（1 行）");
    }

    [Fact]
    public void OnlyDiff_OmitsEqualRows_AndMarksGaps()
    {
        var (left, right) = ModifiedPng();

        var output = new HexDiffOutputFormatter(ColorMode.Never).Format(left, null, right, null, onlyDiff: true);

        var lines = Lines(output);
        lines.Should().NotContain(l => l.StartsWith("  00000000"));
        lines.Should().Contain("  ...");
        lines.Where(l => l.StartsWith("- ") || l.StartsWith("+ ")).Should().HaveCount(2);
    }

    [Fact]
    public void DifferentLengths_ExtraBytesAreDifferences()
    {
        byte[] left = [1, 2, 3];
        byte[] right = [1, 2, 3, 4, 5];

        var formatter = new HexDiffOutputFormatter(ColorMode.Never);
        var output = formatter.Format(left, null, right, null, onlyDiff: true);

        formatter.HasDifferences.Should().BeTrue();
        output.Should().Contain("差分: 2 バイト（1 行）  file1 3 バイト / file2 5 バイト");
    }

    [Fact]
    public void IdenticalFiles_NoDifferences()
    {
        var data = PngTestDataGenerator.CreateMinimalPng();

        var formatter = new HexDiffOutputFormatter(ColorMode.Never);
        var output = formatter.Format(data, null, data, null, onlyDiff: true);

        formatter.HasDifferences.Should().BeFalse();
        output.Should().Contain($"差分なし（{data.Length} バイト）");
        Lines(output).Should().NotContain(l => l.StartsWith("- ") || l.StartsWith("+ "));
    }

    [Fact]
    public void Color_HighlightsDifferingBytes_WithoutCarets()
    {
        var (left, right) = ModifiedPng();

        var output = new HexDiffOutputFormatter(ColorMode.Always).Format(left, null, right, null, onlyDiff: true);

        output.Should().Contain($"\x1b[31m{left[0x13]:X2}\x1b[0m", "file1 の差分バイトは赤");
        output.Should().Contain($"\x1b[32m{right[0x13]:X2}\x1b[0m", "file2 の差分バイトは緑");
        output.Should().NotContain("^^");
    }
}
