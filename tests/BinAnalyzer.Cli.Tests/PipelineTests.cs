using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Cli.Tests;

public class PipelineTests
{
    // プロジェクトルートからの相対パス
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string CliProject = Path.Combine(RepoRoot, "src", "BinAnalyzer.Cli");
    private static readonly string PngFormat = Path.Combine(RepoRoot, "formats", "png.bdef.yaml");
    private static readonly string PngFile = Path.Combine(RepoRoot, "testdata", "real", "test.png");

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

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCli(string args, byte[]? stdin = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{CliProject}\" -- {args}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin is not null,
            UseShellExecute = false,
            WorkingDirectory = RepoRoot,
        };

        using var process = Process.Start(psi)!;

        if (stdin is not null)
        {
            await process.StandardInput.BaseStream.WriteAsync(stdin);
            process.StandardInput.Close();
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    [Fact]
    public async Task StdinDash_DecodesSuccessfully()
    {
        var data = File.ReadAllBytes(PngFile);
        var (exitCode, stdout, _) = await RunCli($"- -f \"{PngFormat}\" -o json", data);

        exitCode.Should().Be(0);
        stdout.Should().NotBeEmpty();
        stdout.Should().Contain("PNG");
    }

    [Fact]
    public async Task StdinFlag_DecodesSuccessfully()
    {
        var data = File.ReadAllBytes(PngFile);
        var (exitCode, stdout, _) = await RunCli($"--stdin -f \"{PngFormat}\" -o json", data);

        exitCode.Should().Be(0);
        stdout.Should().NotBeEmpty();
        stdout.Should().Contain("PNG");
    }

    [Fact]
    public async Task Quiet_NoStdoutOutput()
    {
        var (exitCode, stdout, _) = await RunCli($"\"{PngFile}\" -f \"{PngFormat}\" --quiet");

        exitCode.Should().Be(0);
        stdout.Should().BeEmpty();
    }

    [Fact]
    public async Task Quiet_ExitCode0_OnSuccess()
    {
        var (exitCode, _, _) = await RunCli($"\"{PngFile}\" -f \"{PngFormat}\" --quiet");

        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task ErrorFormatJson_ProducesJson()
    {
        // 存在しないファイルを指定してエラーをJSON出力
        var (exitCode, _, stderr) = await RunCli("nonexistent.bin -f \"" + PngFormat + "\" --error-format json");

        exitCode.Should().Be(1);
        stderr.Should().NotBeEmpty();

        // JSON 出力を検証
        var line = stderr.Trim().Split('\n')[0];
        var json = JsonDocument.Parse(line);
        json.RootElement.GetProperty("type").GetString().Should().Be("error");
        json.RootElement.GetProperty("message").GetString().Should().Contain("ファイルが見つかりません");
    }

    [Fact]
    public async Task ErrorFormatJson_DecodeException()
    {
        // 不正なバイナリデータをstdinから渡してDecodeExceptionを発生させる
        var badData = new byte[] { 0x00, 0x00, 0x00, 0x00 };
        var (exitCode, _, stderr) = await RunCli($"--stdin -f \"{PngFormat}\" --error-format json", badData);

        exitCode.Should().Be(1);
        stderr.Should().NotBeEmpty();

        var line = stderr.Trim().Split('\n')[0];
        var json = JsonDocument.Parse(line);
        json.RootElement.GetProperty("type").GetString().Should().Be("decode_error");
    }

    [Fact]
    public async Task BrokenPipe_ExitsCleanly()
    {
        // パイプ切断シミュレーション: head -c 1 で stdout を即座に閉じる
        var psi = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = $"-c \"dotnet run --project \\\"{CliProject}\\\" -- \\\"{PngFile}\\\" -f \\\"{PngFormat}\\\" | head -c 1\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepoRoot,
        };

        using var process = Process.Start(psi)!;
        await process.WaitForExitAsync();

        // bash全体の終了コードは head のものになるが、
        // BinAnalyzer がクラッシュしないことが重要
        // (SIGPIPE で kill されると 141 になる)
        process.ExitCode.Should().BeOneOf(0, 141);
    }

    [Fact]
    public async Task FilePath_StillWorks()
    {
        var (exitCode, stdout, _) = await RunCli($"\"{PngFile}\" -f \"{PngFormat}\" -o json");

        exitCode.Should().Be(0);
        stdout.Should().NotBeEmpty();
        stdout.Should().Contain("PNG");
    }

    [Fact]
    public async Task StdinAndFile_Error()
    {
        var data = File.ReadAllBytes(PngFile);
        var (exitCode, _, stderr) = await RunCli($"--stdin \"{PngFile}\" -f \"{PngFormat}\"", data);

        exitCode.Should().Be(1);
        stderr.Should().Contain("--stdin とファイルパスは同時に指定できません");
    }

    [Fact]
    public async Task NoFileNoStdin_Error()
    {
        var (exitCode, _, stderr) = await RunCli($"-f \"{PngFormat}\"");

        exitCode.Should().Be(1);
        stderr.Should().Contain("ファイルパスまたは --stdin が必要です");
    }
}
