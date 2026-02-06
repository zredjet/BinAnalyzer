using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class ColorOutputTests
{
    private static readonly string PngFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "png.bdef.yaml");

    private static readonly string BmpFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "bmp.bdef.yaml");

    [Fact]
    public void ColorAlways_ContainsAnsiEscapeCodes()
    {
        var data = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter(ColorMode.Always).Format(decoded);

        // ANSIエスケープコード \x1b[ が含まれること
        output.Should().Contain("\x1b[");
    }

    [Fact]
    public void ColorNever_DoesNotContainAnsiEscapeCodes()
    {
        var data = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter(ColorMode.Never).Format(decoded);

        output.Should().NotContain("\x1b[");
    }

    [Fact]
    public void ColorAlways_IntegerValuesInCyan()
    {
        var data = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter(ColorMode.Always).Format(decoded);

        // シアン色コード \x1b[36m が含まれること（整数値の色）
        output.Should().Contain("\x1b[36m");
    }

    [Fact]
    public void ColorAlways_EnumLabelsInMagenta()
    {
        var data = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter(ColorMode.Always).Format(decoded);

        // マゼンタ色コード \x1b[35m が含まれること（enum値の色）
        output.Should().Contain("\x1b[35m");
        output.Should().Contain("BI_RGB");
    }

    [Fact]
    public void ColorAlways_BytesInYellow()
    {
        var data = PngTestDataGenerator.CreateMinimalPng();
        var format = new YamlFormatLoader().Load(PngFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter(ColorMode.Always).Format(decoded);

        // 黄色コード \x1b[33m が含まれること（バイト列の色）
        output.Should().Contain("\x1b[33m");
    }

    [Fact]
    public void ColorAlways_ValidationPassedInGreen()
    {
        var data = PngTestDataGenerator.CreateMinimalPng();
        var format = new YamlFormatLoader().Load(PngFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter(ColorMode.Always).Format(decoded);

        // 緑色コード \x1b[32m + ✓ が含まれること
        output.Should().Contain("\x1b[32m");
        output.Should().Contain("✓");
    }

    [Fact]
    public void ColorAlways_DimForOffsetsAndConnectors()
    {
        var data = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter(ColorMode.Always).Format(decoded);

        // Dim色コード \x1b[2m が含まれること（オフセット/罫線の色）
        output.Should().Contain("\x1b[2m");
    }

    [Fact]
    public void DefaultConstructor_NoColor()
    {
        var data = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        // デフォルトコンストラクタ（ColorMode.Never）ではカラーコードなし
        var output = new TreeOutputFormatter().Format(decoded);
        output.Should().NotContain("\x1b[");
    }

    [Fact]
    public void HexDump_ColorAlways_ContainsAnsiCodes()
    {
        var data = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new HexDumpOutputFormatter(ColorMode.Always).Format(decoded, data);

        output.Should().Contain("\x1b[");
        output.Should().Contain("\x1b[33m"); // Yellow for hex
        output.Should().Contain("\x1b[36m"); // Cyan for field path
    }

    [Fact]
    public void HexDump_ColorNever_NoAnsiCodes()
    {
        var data = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new HexDumpOutputFormatter(ColorMode.Never).Format(decoded, data);

        output.Should().NotContain("\x1b[");
    }

    [Fact]
    public void Diff_ColorAlways_ContainsAnsiCodes()
    {
        var decoded1 = new BinAnalyzer.Core.Decoded.DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 4,
            Children = [new BinAnalyzer.Core.Decoded.DecodedInteger { Name = "value", Offset = 0, Size = 4, Value = 100 }],
        };
        var decoded2 = new BinAnalyzer.Core.Decoded.DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 4,
            Children = [new BinAnalyzer.Core.Decoded.DecodedInteger { Name = "value", Offset = 0, Size = 4, Value = 200 }],
        };
        var diff = DiffEngine.Compare(decoded1, decoded2);
        var output = new DiffOutputFormatter(ColorMode.Always).Format(diff);

        output.Should().Contain("\x1b[");
        output.Should().Contain("\x1b[33m"); // Yellow for changed
    }

    [Fact]
    public void Diff_ColorNever_NoAnsiCodes()
    {
        var decoded1 = new BinAnalyzer.Core.Decoded.DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 4,
            Children = [new BinAnalyzer.Core.Decoded.DecodedInteger { Name = "value", Offset = 0, Size = 4, Value = 100 }],
        };
        var decoded2 = new BinAnalyzer.Core.Decoded.DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 4,
            Children = [new BinAnalyzer.Core.Decoded.DecodedInteger { Name = "value", Offset = 0, Size = 4, Value = 200 }],
        };
        var diff = DiffEngine.Compare(decoded1, decoded2);
        var output = new DiffOutputFormatter(ColorMode.Never).Format(diff);

        output.Should().NotContain("\x1b[");
    }
}
