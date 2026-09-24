using System.Text.Json;
using BinAnalyzer.Engine;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Cli.Tests;

/// <summary>REQ-164: patch サブコマンド。</summary>
[Collection("CliTests")]
public class PatchCommandTests : IDisposable
{
    private static readonly string RepoRoot = CliRunner.RepoRoot;
    private static readonly string PngFormat = Path.Combine(RepoRoot, "formats", "png.bdef.yaml");

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"binanalyzer-req164-{Guid.NewGuid():N}");
    private readonly string _input;

    public PatchCommandTests()
    {
        Directory.CreateDirectory(_dir);
        _input = Path.Combine(_dir, "in.png");
        File.WriteAllBytes(_input, MinimalPng());
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static byte[] MinimalPng()
    {
        var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(ms, "IHDR", [0, 0, 0, 1, 0, 0, 0, 1, 8, 2, 0, 0, 0]);
        WriteChunk(ms, "IDAT", [0x78, 0x9C, 0x63, 0x60, 0x60, 0x60, 0x00, 0x00, 0x00, 0x04, 0x00, 0x01]);
        WriteChunk(ms, "IEND", []);
        return ms.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var len = data.Length;
        s.Write([(byte)(len >> 24), (byte)(len >> 16), (byte)(len >> 8), (byte)len]);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        var crc = Crc32Calculator.Compute(typeBytes.Concat(data).ToArray());
        s.Write([(byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc]);
    }

    private static Task<(int ExitCode, string StdOut, string StdErr)> RunCli(params string[] args) =>
        CliRunner.RunAsync(args);

    private static async Task<JsonElement> DecodeIhdrAsync(string file)
    {
        var (exit, stdout, stderr) = await RunCli(file, "-f", PngFormat, "-o", "json");
        exit.Should().Be(0, stderr);
        var doc = JsonDocument.Parse(stdout);
        return doc.RootElement.GetProperty("children").GetProperty("chunks").GetProperty("elements")[0].GetProperty("children");
    }

    [Fact]
    public async Task Patch_SetInteger_WritesOutput_RecalculatesCrc_AndDecodes()
    {
        var output = Path.Combine(_dir, "out.png");

        var (exit, stdout, stderr) = await RunCli("patch", _input, "-f", PngFormat, "--set", "chunks[0].data.width=2", "-o", output);

        exit.Should().Be(0, stderr);
        stdout.Should().Contain("chunks[0].data.width").And.Contain("1 → 2").And.Contain("00 00 00 01 → 00 00 00 02")
            .And.Contain("chunks[0].crc (CRC-32)").And.Contain("書き込みました");
        stderr.Should().NotContain("警告");
        File.ReadAllBytes(_input).Should().Equal(MinimalPng(), "元ファイルは変更しない");
        File.Exists(output).Should().BeTrue();

        var ihdr = await DecodeIhdrAsync(output);
        ihdr.GetProperty("data").GetProperty("children").GetProperty("width").GetProperty("value").GetInt64().Should().Be(2);
        ihdr.GetProperty("crc").GetProperty("checksum_valid").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Patch_HexInteger_EnumLabel_StringAndBytes_MultipleSets()
    {
        var output = Path.Combine(_dir, "multi.png");

        var (exit, stdout, stderr) = await RunCli("patch", _input, "-f", PngFormat,
            "--set", "chunks[0].data.width=0x10",
            "--set", "chunks[0].data.color_type=truecolor_alpha",
            "--set", "signature=89 50 4E 47 0D 0A 1A 0B",
            "--set", "chunks[2].type=IENX",
            "-o", output);

        exit.Should().Be(0, stderr);
        stdout.Should().Contain("2 (truecolor) → truecolor_alpha").And.Contain("02 → 06");
        var bytes = File.ReadAllBytes(output);
        bytes[7].Should().Be(0x0B);
        bytes[0x13].Should().Be(0x10);
        bytes[0x19].Should().Be(6);
        System.Text.Encoding.ASCII.GetString(bytes, 0x3D, 4).Should().Be("IENX");

        var (exit2, stdout2, _) = await RunCli(output, "-f", PngFormat, "-o", "json");
        exit2.Should().Be(0);
        stdout2.Should().NotContain("\"checksum_valid\":false", "IHDR と IEND の crc が両方再計算されている");
    }

    [Fact]
    public async Task Patch_DryRun_PrintsChanges_AndWritesNothing()
    {
        var output = Path.Combine(_dir, "never.png");

        var (exit, stdout, stderr) = await RunCli("patch", _input, "-f", PngFormat, "--set", "chunks[0].data.width=2", "-o", output, "--dry-run");

        exit.Should().Be(0, stderr);
        stdout.Should().Contain("[dry-run] chunks[0].data.width").And.Contain("[dry-run] チェックサム再計算: 1 件").And.Contain("ファイルは作成しません");
        File.Exists(output).Should().BeFalse();
        File.ReadAllBytes(_input).Should().Equal(MinimalPng());

        // -o 無しでも dry-run は可
        var (exit2, _, _) = await RunCli("patch", _input, "-f", PngFormat, "--set", "chunks[0].data.width=2", "--dry-run");
        exit2.Should().Be(0);
        Directory.GetFiles(_dir).Should().ContainSingle();
    }

    [Fact]
    public async Task Patch_UnknownPath_FailsWithSuggestion()
    {
        var (exit, _, stderr) = await RunCli("patch", _input, "-f", PngFormat, "--set", "width=2", "--dry-run");
        exit.Should().Be(1);
        stderr.Should().Contain("フィールドが見つかりません: width").And.Contain("chunks[0].data.width");

        var (exit2, _, stderr2) = await RunCli("patch", _input, "-f", PngFormat, "--set", "chunks[9].data.width=2", "--dry-run", "--error-format", "json");
        exit2.Should().Be(1);
        stderr2.Should().Contain("\"type\":\"error\"").And.Contain("chunks[9].data.width");
    }

    [Fact]
    public async Task Patch_OutOfRange_NonEditable_MissingOutput_SameOutput_Fail()
    {
        var (e1, _, s1) = await RunCli("patch", _input, "-f", PngFormat, "--set", "chunks[0].data.bit_depth=300", "--dry-run");
        e1.Should().Be(1);
        s1.Should().Contain("範囲外");

        var (e2, _, s2) = await RunCli("patch", _input, "-f", PngFormat, "--set", "chunks[0]=2", "--dry-run");
        e2.Should().Be(1);
        s2.Should().Contain("書き換えできません");

        var (e3, _, s3) = await RunCli("patch", _input, "-f", PngFormat, "--set", "chunks[0].data.width=2");
        e3.Should().Be(1);
        s3.Should().Contain("-o");

        var (e4, _, s4) = await RunCli("patch", _input, "-f", PngFormat, "--set", "chunks[0].data.width=2", "-o", _input);
        e4.Should().Be(1);
        s4.Should().Contain("同じ");
        File.ReadAllBytes(_input).Should().Equal(MinimalPng());

        var (e5, _, s5) = await RunCli("patch", _input, "-f", PngFormat, "--set", "novalue", "--dry-run");
        e5.Should().Be(1);
        s5.Should().Contain("形式");

        var (e6, _, s6) = await RunCli("patch", _input, "-f", PngFormat, "--set", "chunks[0].data.width=1", "--set", "chunks[0].data.width=2", "--dry-run");
        e6.Should().Be(1);
        s6.Should().Contain("重なって");
    }

    [Fact]
    public async Task Patch_NoChecksum_LeavesCrcStale_AndWarns()
    {
        var output = Path.Combine(_dir, "stale.png");

        var (exit, stdout, stderr) = await RunCli("patch", _input, "-f", PngFormat, "--set", "chunks[0].data.width=2", "-o", output, "--no-checksum");

        exit.Should().Be(0);
        stdout.Should().Contain("スキップ（--no-checksum）");
        stderr.Should().Contain("チェックサム不一致が 1 件");
        var ihdr = await DecodeIhdrAsync(output);
        ihdr.GetProperty("crc").GetProperty("checksum_valid").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task DiffHexdump_ShowsPatchedBytes_AndExitCodes()
    {
        // REQ-156: patch で書き換えた結果を diff --output hexdump で確認する
        var output = Path.Combine(_dir, "out.png");
        (await RunCli("patch", _input, "-f", PngFormat, "--set", "chunks[0].data.width=2", "-o", output)).ExitCode.Should().Be(0);

        var (exit, stdout, _) = await RunCli("diff", _input, output, "-f", PngFormat, "--output", "hexdump", "--only-diff", "--color", "never");

        exit.Should().Be(1, "差分あり");
        stdout.Should().Contain("- 00000010").And.Contain("+ 00000010").And.Contain("chunks[0].data.width");
        stdout.Should().Contain("差分: 5 バイト", "width の 1 バイトと再計算された CRC の 4 バイト");

        var (same, _, _) = await RunCli("diff", _input, _input, "-f", PngFormat, "--output", "hexdump", "--color", "never");
        same.Should().Be(0);
    }
}
