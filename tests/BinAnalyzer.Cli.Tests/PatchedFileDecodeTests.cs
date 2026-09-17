using System.Diagnostics;
using System.Text.Json;
using BinAnalyzer.Core.Patching;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Cli.Tests;

/// <summary>REQ-169 受入条件 5: 編集・保存したファイルを CLI でデコードすると変更が反映され、チェックサムも通る。</summary>
[Collection("CliTests")]
public class PatchedFileDecodeTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string CliProject = Path.Combine(RepoRoot, "src", "BinAnalyzer.Cli");
    private static readonly string PngFormat = Path.Combine(RepoRoot, "formats", "png.bdef.yaml");

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir, "src")) && Directory.Exists(Path.Combine(dir, "formats")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("リポジトリルートが見つかりません");
    }

    /// <summary>1x1 の最小 PNG（signature + IHDR + IDAT + IEND）。</summary>
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

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCli(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{CliProject}\" -- {args}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepoRoot,
        };
        using var process = Process.Start(psi)!;
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    [Fact]
    public async Task PatchedPng_DecodesWithCli_WidthChangedAndCrcValid()
    {
        var format = new YamlFormatLoader().Load(PngFormat);
        // IHDR width（オフセット 16、u32 BE）を 1 → 2 に。crc（オフセット 29）は BinaryPatcher が再計算する
        var patched = BinaryPatcher.Apply(MinimalPng(), format, [new BytePatch(16, [0, 0, 0, 2])]);
        patched.ChecksumFixes.Select(f => f.Path).Should().Equal("chunks[0].crc");

        var tmp = Path.Combine(Path.GetTempPath(), $"binanalyzer-req169-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(tmp, patched.Data);
        try
        {
            var (exit, stdout, stderr) = await RunCli($"\"{tmp}\" -f \"{PngFormat}\" -o json");
            exit.Should().Be(0, stderr);

            using var doc = JsonDocument.Parse(stdout);
            // struct の children は名前をキーにしたオブジェクト、array の elements は配列
            var ihdr = doc.RootElement.GetProperty("children").GetProperty("chunks").GetProperty("elements")[0].GetProperty("children");
            ihdr.GetProperty("data").GetProperty("children").GetProperty("width").GetProperty("value").GetInt64().Should().Be(2);
            ihdr.GetProperty("crc").GetProperty("checksum_valid").GetBoolean().Should().BeTrue();
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
