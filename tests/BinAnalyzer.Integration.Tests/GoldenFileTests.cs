using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace BinAnalyzer.Integration.Tests;

[Trait("Category", "Golden")]
public class GoldenFileTests : IClassFixture<RealFileFixture>
{
    private static readonly string FormatsDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats"));

    private static readonly string GoldenDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "testdata", "golden"));

    private readonly RealFileFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly YamlFormatLoader _loader = new();
    private readonly BinaryDecoder _decoder = new();

    public GoldenFileTests(RealFileFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Theory]
    [InlineData("png", "png.bdef.yaml", "test.png")]
    [InlineData("bmp", "bmp.bdef.yaml", "test.bmp")]
    [InlineData("gif", "gif.bdef.yaml", "test.gif")]
    [InlineData("jpeg", "jpeg.bdef.yaml", "test.jpg")]
    [InlineData("tiff", "tiff.bdef.yaml", "test.tiff")]
    [InlineData("ico", "ico.bdef.yaml", "test.ico")]
    [InlineData("webp", "webp.bdef.yaml", "test.webp")]
    [InlineData("wav", "wav.bdef.yaml", "test.wav")]
    [InlineData("mp3", "mp3.bdef.yaml", "test.mp3")]
    [InlineData("flac", "flac.bdef.yaml", "test.flac")]
    [InlineData("midi", "midi.bdef.yaml", "test.midi")]
    [InlineData("avi", "avi.bdef.yaml", "test.avi")]
    [InlineData("mp4", "mp4.bdef.yaml", "test.mp4")]
    [InlineData("flv", "flv.bdef.yaml", "test.flv")]
    [InlineData("elf", "elf.bdef.yaml", "test.elf")]
    [InlineData("pe", "pe.bdef.yaml", "test.pe")]
    [InlineData("macho", "macho.bdef.yaml", "test.macho")]
    [InlineData("wasm", "wasm.bdef.yaml", "test.wasm")]
    [InlineData("java-class", "java-class.bdef.yaml", "test.class")]
    [InlineData("zip", "zip.bdef.yaml", "test.zip")]
    [InlineData("gzip", "gzip.bdef.yaml", "test.gz")]
    [InlineData("tar", "tar.bdef.yaml", "test.tar")]
    [InlineData("lz4", "lz4.bdef.yaml", "test.lz4")]
    [InlineData("7z", "7z.bdef.yaml", "test.7z")]
    [InlineData("sqlite", "sqlite.bdef.yaml", "test.sqlite")]
    [InlineData("pdf", "pdf.bdef.yaml", "test.pdf")]
    [InlineData("pcap", "pcap.bdef.yaml", "test.pcap")]
    [InlineData("dns", "dns.bdef.yaml", "test.dns")]
    [InlineData("otf", "otf.bdef.yaml", "test.otf")]
    [InlineData("icc", "icc.bdef.yaml", "test.icc")]
    [InlineData("parquet", "parquet.bdef.yaml", "test.parquet")]
    [InlineData("protobuf", "protobuf.bdef.yaml", "test.protobuf")]
    [InlineData("ogg", "ogg.bdef.yaml", "test.ogg")]
    [InlineData("heif", "heif.bdef.yaml", "test.heif")]
    [InlineData("msgpack", "msgpack.bdef.yaml", "test.msgpack")]
    [InlineData("cbor", "cbor.bdef.yaml", "test.cbor")]
    public void DecodeResult_MatchesGoldenFile(string name, string formatFile, string binaryFile)
    {
        // 1. フォーマット読込 + バイナリデコード
        var filePath = Path.Combine(_fixture.TestDataDir, binaryFile);
        if (!File.Exists(filePath))
        {
            Assert.Fail($"Test file not found: {binaryFile} — RealFileFixture should have generated it");
        }

        var data = File.ReadAllBytes(filePath);
        var formatPath = Path.Combine(FormatsDir, formatFile);
        var format = _loader.Load(formatPath);

        _output.WriteLine($"Format: {name}");
        _output.WriteLine($"File: {binaryFile} ({data.Length} bytes)");

        // 2. デコード + JSON シリアライズ
        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);
        var actual = new JsonOutputFormatter().Format(result.Root);

        // 3. ゴールデンファイル更新 or 比較
        var goldenPath = Path.Combine(GoldenDir, $"{name}.json");

        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(GoldenDir);
            File.WriteAllText(goldenPath, actual);
            _output.WriteLine($"Updated golden file: {goldenPath}");
            return;
        }

        if (!File.Exists(goldenPath))
        {
            Assert.Fail(
                $"ゴールデンファイル {name}.json が存在しません。UPDATE_GOLDEN=1 で生成してください。");
        }

        var expected = File.ReadAllText(goldenPath);
        // Use Assert.Equal instead of FluentAssertions to avoid FormatException with JSON braces
        Assert.Equal(expected, actual);

        _output.WriteLine("Golden file match: OK");
    }
}
