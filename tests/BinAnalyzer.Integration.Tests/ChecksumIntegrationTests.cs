using System.Text.Json;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class ChecksumIntegrationTests
{
    private static readonly string PngFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "png.bdef.yaml");

    [Fact]
    public void Png_ValidCrc_ShowsCheckmark()
    {
        var pngData = PngTestDataGenerator.CreateMinimalPng();
        var format = new YamlFormatLoader().Load(PngFormatPath);
        var decoded = new BinaryDecoder().Decode(pngData, format);
        var output = new TreeOutputFormatter().Format(decoded);

        // CRC検証が成功していること（✓が表示される）
        output.Should().Contain("✓ (CRC-32)");
        // CRC検証失敗（✗）がないこと
        output.Should().NotContain("✗ (CRC-32");
    }

    [Fact]
    public void Png_ValidCrc_JsonOutput_ShowsValid()
    {
        var pngData = PngTestDataGenerator.CreateMinimalPng();
        var format = new YamlFormatLoader().Load(PngFormatPath);
        var decoded = new BinaryDecoder().Decode(pngData, format);
        var json = new JsonOutputFormatter().Format(decoded);

        using var doc = JsonDocument.Parse(json);
        // JSON内に checksum_valid: true が含まれていること
        json.Should().Contain("\"checksum_valid\": true");
        json.Should().NotContain("\"checksum_valid\": false");
    }
}
