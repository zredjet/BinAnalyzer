using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Cli.Tests;

[Collection("CliTests")]
public class PipelineTests
{
    // プロジェクトルートからの相対パス
    private static readonly string RepoRoot = CliRunner.RepoRoot;
    private static readonly string PngFormat = Path.Combine(RepoRoot, "formats", "png.bdef.yaml");
    // testdata/real は .gitignore 対象で CI に無いので、コミット済みのスモーク用サンプルを使う
    private static readonly string PngFile = Path.Combine(RepoRoot, "testdata", "smoke", "sample.png");

    private static Task<(int ExitCode, string StdOut, string StdErr)> RunCli(string args, byte[]? stdin = null) =>
        CliRunner.RunAsync(args, stdin);

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
        // bash -c の $0 に dotnet ホスト、$@ に CLI の dll と引数を渡す（クォートの入れ子を避ける）
        var psi = CliRunner.CreateStartInfo("bash");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("\"$0\" \"$@\" | head -c 1");
        psi.ArgumentList.Add(CliRunner.DotnetHost);
        psi.ArgumentList.Add(CliRunner.CliAssemblyPath);
        psi.ArgumentList.Add(PngFile);
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add(PngFormat);

        var (exitCode, _, _) = await CliRunner.RunAsync(psi);

        // bash全体の終了コードは head のものになるが、
        // BinAnalyzer がクラッシュしないことが重要
        // (SIGPIPE で kill されると 141 になる)
        exitCode.Should().BeOneOf(0, 141);
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
