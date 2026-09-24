using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Cli.Tests;

[Collection("CliTests")]
public class ValidateCommandTests : IDisposable
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string CliProject = Path.Combine(RepoRoot, "src", "BinAnalyzer.Cli");

    private readonly List<string> _tempFiles = new();

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

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCli(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            // --no-build は付けない: 同じコレクションの他クラスが先に Debug ビルドしている前提になり、実行順で CI が落ちうる
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

    private string CreateTempFormatFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.bdef.yaml");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { }
        }
    }

    // 正常なフォーマット定義（エラー・警告なし）
    private const string ValidFormat = """
        root: header
        structs:
          header:
            fields:
              - name: magic
                type: bytes
                size: 4
        """;

    // エラーのあるフォーマット定義（VAL001: struct参照未指定）
    private const string InvalidFormat = """
        root: root
        structs:
          root:
            fields:
              - name: nested
                type: struct
        """;

    // 警告のみのフォーマット定義（VAL109: 到達不可能なstruct）
    private const string WarningOnlyFormat = """
        root: header
        structs:
          header:
            fields:
              - name: magic
                type: bytes
                size: 4
          unused_struct:
            fields:
              - name: data
                type: bytes
                size: 4
        """;

    [Fact]
    public async Task ValidFormat_ExitCode0()
    {
        var file = CreateTempFormatFile(ValidFormat);
        var (exitCode, _, _) = await RunCli($"validate \"{file}\"");

        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task ValidFormat_OutputContainsOk()
    {
        var file = CreateTempFormatFile(ValidFormat);
        var (_, stdout, _) = await RunCli($"validate \"{file}\"");

        stdout.Should().Contain("OK");
    }

    [Fact]
    public async Task InvalidFormat_ExitCode1()
    {
        var file = CreateTempFormatFile(InvalidFormat);
        var (exitCode, _, _) = await RunCli($"validate \"{file}\"");

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task InvalidFormat_OutputContainsErrorCode()
    {
        var file = CreateTempFormatFile(InvalidFormat);
        var (_, stdout, _) = await RunCli($"validate \"{file}\"");

        stdout.Should().Contain("VAL001");
    }

    [Fact]
    public async Task MultipleFiles_AllValidated()
    {
        var file1 = CreateTempFormatFile(ValidFormat);
        var file2 = CreateTempFormatFile(ValidFormat);
        var (exitCode, stdout, _) = await RunCli($"validate \"{file1}\" \"{file2}\"");

        exitCode.Should().Be(0);
        stdout.Should().Contain("サマリー");
        stdout.Should().Contain("OK: 2");
    }

    [Fact]
    public async Task MultipleFiles_OneInvalid_ExitCode1()
    {
        var validFile = CreateTempFormatFile(ValidFormat);
        var invalidFile = CreateTempFormatFile(InvalidFormat);
        var (exitCode, stdout, _) = await RunCli($"validate \"{validFile}\" \"{invalidFile}\"");

        exitCode.Should().Be(1);
        stdout.Should().Contain("NG: 1");
        stdout.Should().Contain("OK: 1");
    }

    [Fact]
    public async Task NonexistentFile_ExitCode1()
    {
        var (exitCode, stdout, _) = await RunCli("validate nonexistent_file.bdef.yaml");

        exitCode.Should().Be(1);
        stdout.Should().Contain("ファイルが見つかりません");
    }

    [Fact]
    public async Task JsonFormat_ValidJson()
    {
        var file = CreateTempFormatFile(ValidFormat);
        var (exitCode, stdout, _) = await RunCli($"validate \"{file}\" --format json");

        exitCode.Should().Be(0);

        // JSON配列としてパース可能
        var json = JsonDocument.Parse(stdout);
        json.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        json.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task JsonFormat_ContainsFileAndValid()
    {
        var file = CreateTempFormatFile(InvalidFormat);
        var (_, stdout, _) = await RunCli($"validate \"{file}\" --format json");

        var json = JsonDocument.Parse(stdout);
        var entry = json.RootElement[0];

        entry.GetProperty("file").GetString().Should().NotBeNullOrEmpty();
        entry.GetProperty("valid").GetBoolean().Should().BeFalse();
        entry.GetProperty("errors").GetArrayLength().Should().BeGreaterThan(0);
        entry.TryGetProperty("warnings", out _).Should().BeTrue();
    }

    [Fact]
    public async Task WarningsAsErrors_WarningCausesExitCode1()
    {
        var file = CreateTempFormatFile(WarningOnlyFormat);

        // 警告のみ → --warnings-as-errors なしでは exit 0
        var (exitCode0, _, _) = await RunCli($"validate \"{file}\"");
        exitCode0.Should().Be(0);

        // 警告のみ → --warnings-as-errors ありでは exit 1
        var (exitCode1, stdout, _) = await RunCli($"validate \"{file}\" --warnings-as-errors");
        exitCode1.Should().Be(1);
        stdout.Should().Contain("NG");
    }

    [Fact]
    public async Task ParseError_ReportsGracefully()
    {
        var file = CreateTempFormatFile("invalid: yaml: content: [broken");
        var (exitCode, stdout, _) = await RunCli($"validate \"{file}\"");

        exitCode.Should().Be(1);
        stdout.Should().Contain("NG");
    }

    // DSL の未知キー（REQ-184）: 書き間違いは VAL123 の警告になる
    private const string TypoFormat = """
        name: t
        root: s
        structs:
          s:
            fields:
              - name: magic
                type: bytes
                size: "4"
                expect: [0x4D]
                repeat_cout: 3
        """;

    [Fact]
    public async Task UnknownKeys_ReportVAL123WithLineAndSuggestion()
    {
        var file = CreateTempFormatFile(TypoFormat);
        var name = Path.GetFileName(file);

        var (exitCode, stdout, _) = await RunCli($"validate \"{file}\"");

        exitCode.Should().Be(0);
        stdout.Should().Contain("警告 2 件");
        stdout.Should().Contain($"警告 [VAL123]: struct 's' のフィールド 'magic' の未知のキー 'expect' は無視されます（もしかして 'expected'?） ({name}:9)");
        stdout.Should().Contain($"警告 [VAL123]: struct 's' のフィールド 'magic' の未知のキー 'repeat_cout' は無視されます（もしかして 'repeat_count'?） ({name}:10)");
    }

    [Fact]
    public async Task UnknownKeys_WarningsAsErrors_ExitCode1()
    {
        var file = CreateTempFormatFile(TypoFormat);

        var (exitCode, _, _) = await RunCli($"validate \"{file}\" --warnings-as-errors");

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task UnknownKeys_JsonHasFileAndLine()
    {
        var file = CreateTempFormatFile(TypoFormat);

        var (_, stdout, _) = await RunCli($"validate \"{file}\" --format json");

        var warnings = JsonDocument.Parse(stdout).RootElement[0].GetProperty("warnings");
        warnings.GetArrayLength().Should().Be(2);
        var first = warnings[0];
        first.GetProperty("code").GetString().Should().Be("VAL123");
        first.GetProperty("struct").GetString().Should().Be("s");
        first.GetProperty("field").GetString().Should().Be("magic");
        first.GetProperty("file").GetString().Should().Be(file);
        first.GetProperty("line").GetInt32().Should().Be(9);
    }
}
