using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace BinAnalyzer.Integration.Tests;

[Trait("Category", "RealFile")]
public class RealFileValidationTests : IClassFixture<RealFileFixture>
{
    private static readonly string FormatsDir =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats");

    private readonly RealFileFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly YamlFormatLoader _loader = new();
    private readonly BinaryDecoder _decoder = new();

    public RealFileValidationTests(RealFileFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// 全30フォーマットの実ファイル E2E 検証。
    /// formatFile: .bdef.yaml ファイル名
    /// fileName: testdata/real/ 内のテストファイル名
    /// expectedName: フォーマット定義の name フィールド
    /// minChildren: ルート構造の最小 Children 数
    /// </summary>
    [Theory]
    [InlineData("png.bdef.yaml", "test.png", "PNG", 2)]
    [InlineData("jpeg.bdef.yaml", "test.jpg", "JPEG", 1)]
    [InlineData("gif.bdef.yaml", "test.gif", "GIF", 3)]
    [InlineData("bmp.bdef.yaml", "test.bmp", "BMP", 1)]
    [InlineData("tiff.bdef.yaml", "test.tiff", "TIFF", 2)]
    [InlineData("ico.bdef.yaml", "test.ico", "ICO", 2)]
    [InlineData("webp.bdef.yaml", "test.webp", "WebP", 2)]
    [InlineData("wav.bdef.yaml", "test.wav", "WAV", 2)]
    [InlineData("mp3.bdef.yaml", "test.mp3", "MP3", 1)]
    [InlineData("flac.bdef.yaml", "test.flac", "FLAC", 2)]
    [InlineData("midi.bdef.yaml", "test.midi", "MIDI", 2)]
    [InlineData("elf.bdef.yaml", "test.elf", "ELF", 1)]
    [InlineData("pe.bdef.yaml", "test.pe", "PE", 1)]
    [InlineData("macho.bdef.yaml", "test.macho", "Mach-O", 2)]
    [InlineData("java-class.bdef.yaml", "test.class", "JavaClass", 1)]
    [InlineData("zip.bdef.yaml", "test.zip", "ZIP", 1)]
    [InlineData("gzip.bdef.yaml", "test.gz", "GZIP", 1)]
    [InlineData("tar.bdef.yaml", "test.tar", "TAR", 1)]
    [InlineData("lz4.bdef.yaml", "test.lz4", "LZ4", 2)]
    [InlineData("pcap.bdef.yaml", "test.pcap", "PCAP", 2)]
    [InlineData("sqlite.bdef.yaml", "test.sqlite", "SQLite", 1)]
    [InlineData("pdf.bdef.yaml", "test.pdf", "PDF", 1)]
    [InlineData("otf.bdef.yaml", "test.otf", "OTF", 2)]
    [InlineData("icc.bdef.yaml", "test.icc", "ICC", 2)]
    [InlineData("avi.bdef.yaml", "test.avi", "AVI", 2)]
    [InlineData("flv.bdef.yaml", "test.flv", "FLV", 2)]
    [InlineData("7z.bdef.yaml", "test.7z", "7z", 2)]
    [InlineData("wasm.bdef.yaml", "test.wasm", "WASM", 2)]
    [InlineData("dns.bdef.yaml", "test.dns", "DNS", 2)]
    [InlineData("parquet.bdef.yaml", "test.parquet", "Parquet", 2)]
    public void RealFile_DecodesSuccessfully(
        string formatFile, string fileName, string expectedName, int minChildren)
    {
        // 1. ファイル読み込み（RealFileFixture が全ファイルを保証するが念のためガード）
        var filePath = Path.Combine(_fixture.TestDataDir, fileName);
        if (!File.Exists(filePath))
        {
            _output.WriteLine($"SKIPPED: Test file not found: {fileName}");
            return;
        }

        var data = File.ReadAllBytes(filePath);
        var formatPath = Path.Combine(FormatsDir, formatFile);
        var format = _loader.Load(formatPath);

        _output.WriteLine($"Format: {expectedName}");
        _output.WriteLine($"File: {fileName} ({data.Length} bytes)");

        // 2. DecodeWithRecovery(ErrorMode.Continue) でデコード
        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);

        // 3. アサーション
        result.Root.Should().NotBeNull();
        result.Root.Name.Should().Be(expectedName);
        result.Root.Children.Count.Should().BeGreaterThanOrEqualTo(minChildren,
            $"{expectedName}: expected at least {minChildren} children but got {result.Root.Children.Count}");

        // ツリー出力が空でなく、フォーマット名を含むこと
        var formatter = new TreeOutputFormatter();
        var treeOutput = formatter.Format(result.Root);
        treeOutput.Should().NotBeNullOrWhiteSpace();
        treeOutput.Should().Contain(expectedName);

        _output.WriteLine($"Children: {result.Root.Children.Count}");
        _output.WriteLine($"Tree output length: {treeOutput.Length} chars");

        // 4. エラーがあればログ（テスト失敗にはしない）
        if (result.Errors.Count > 0)
        {
            _output.WriteLine($"Decode errors ({result.Errors.Count}):");
            foreach (var error in result.Errors)
            {
                _output.WriteLine($"  [{error.Offset:X8}] {error.FieldPath}: {error.Message}");
            }
        }
        else
        {
            _output.WriteLine("No decode errors.");
        }
    }
}
