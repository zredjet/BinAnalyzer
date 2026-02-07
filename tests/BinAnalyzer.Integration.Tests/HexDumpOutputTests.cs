using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class HexDumpOutputTests
{
    private static readonly string BmpFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "bmp.bdef.yaml");

    private static readonly string PngFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "png.bdef.yaml");

    [Fact]
    public void HexDump_ContainsOffsetColumn()
    {
        var data = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new HexDumpOutputFormatter().Format(decoded, data);

        output.Should().Contain("00000000");
    }

    [Fact]
    public void HexDump_ContainsHexBytes()
    {
        var data = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new HexDumpOutputFormatter().Format(decoded, data);

        // BMP signature: 42 4D
        output.Should().Contain("42 4D");
    }

    [Fact]
    public void HexDump_ContainsAsciiDisplay()
    {
        var data = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new HexDumpOutputFormatter().Format(decoded, data);

        // BM signature in ASCII
        output.Should().Contain("BM");
    }

    [Fact]
    public void HexDump_ContainsFieldNames()
    {
        var data = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new HexDumpOutputFormatter().Format(decoded, data);

        output.Should().Contain("file_header.signature");
        output.Should().Contain("file_header.file_size");
        output.Should().Contain("header_size");
    }

    [Fact]
    public void HexDump_NonPrintableBytesShownAsDot()
    {
        var data = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new HexDumpOutputFormatter().Format(decoded, data);

        // 非印字文字は '.' で表示される
        // BMP先頭行: 42 4D (BM) の後に制御文字があるので '.' が含まれるはず
        var lines = output.Split('\n');
        var dataLines = lines.Where(l => l.StartsWith("0000")).ToArray();
        dataLines.Should().NotBeEmpty();
        // どこかの行に '.' が含まれるはず
        dataLines.Should().Contain(l => l.Contains('.'));
    }

    [Fact]
    public void HexDump_ContainsHeaderLine()
    {
        var data = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new HexDumpOutputFormatter().Format(decoded, data);

        output.Should().Contain("Offset");
        output.Should().Contain("ASCII");
        output.Should().Contain("Field");
    }

    [Fact]
    public void HexDump_NestedFieldPath_ContainsDotNotation()
    {
        var data = PngTestDataGenerator.CreateMinimalPng();
        var format = new YamlFormatLoader().Load(PngFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new HexDumpOutputFormatter().Format(decoded, data);

        // PNGのチャンク構造: chunks[0].length, chunks[0].type 等
        output.Should().Contain("chunks[0]");
        output.Should().Contain("signature");
    }

    [Fact]
    public void HexDump_Bmp_CoversAllBytes()
    {
        var data = BmpTestDataGenerator.CreateMinimalBmp();
        var format = new YamlFormatLoader().Load(BmpFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new HexDumpOutputFormatter().Format(decoded, data);

        // 58バイトのBMP → pixel_data部分まで含まれる
        output.Should().Contain("pixel_data");
    }
}
