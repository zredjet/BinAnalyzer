using System.Diagnostics;
using System.Reflection;

namespace BinAnalyzer.Cli.Tests;

/// <summary>
/// ビルド済みの BinAnalyzer.Cli.dll を <c>dotnet &lt;dll&gt;</c> で起動するテスト用ヘルパー。
/// </summary>
/// <remarks>
/// 以前は各テストが <c>dotnet run --project</c> で CLI を起動していたが、<c>dotnet run</c> は毎回ビルドを走らせる。
/// 外側の <c>dotnet test</c>（ビルドあり）と入れ子になると、ノード再利用の MSBuild ワーカーがリダイレクトした
/// stdout/stderr を継承したまま生き残り、ReadToEnd が終わらずテストがハングすることがあった。
/// CLI はテストプロジェクトの ProjectReference として同じ構成でビルドされるので、その出力 dll を直接起動する
/// （apphost は mise 等で入れたランタイムを見つけられないことがあるため <c>dotnet &lt;dll&gt;</c> を使う）。
/// </remarks>
internal static class CliRunner
{
    /// <summary>1 プロセスあたりの上限。超えたらプロセスツリーを kill してテストを失敗させる。</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    public static string RepoRoot { get; } = FindRepoRoot();

    /// <summary>テスト実行中の dotnet ホスト（<c>dotnet test</c> が設定する）。無ければ PATH 上の dotnet。</summary>
    public static string DotnetHost { get; } = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host && File.Exists(host)
        ? host
        : "dotnet";

    /// <summary>ビルド時に csproj が埋め込んだ BinAnalyzer.Cli.dll のパス。</summary>
    public static string CliAssemblyPath { get; } = ResolveCliAssemblyPath();

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

    private static string ResolveCliAssemblyPath()
    {
        var path = typeof(CliRunner).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "CliAssemblyPath")?.Value;
        if (string.IsNullOrEmpty(path))
            throw new InvalidOperationException("CliAssemblyPath メタデータがありません（BinAnalyzer.Cli.Tests.csproj の EmbedCliAssemblyPath ターゲットを確認）");
        if (!File.Exists(path))
            throw new InvalidOperationException($"CLI がビルドされていません: {path}（テストと同じ構成で BinAnalyzer.Cli をビルドしてください）");
        return path;
    }

    /// <summary>引数をコマンドライン文字列のまま渡す（呼び出し側でクォート済み）。</summary>
    public static Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(string arguments, byte[]? stdin = null)
    {
        var psi = CreateStartInfo(DotnetHost);
        psi.Arguments = $"\"{CliAssemblyPath}\" {arguments}";
        return RunAsync(psi, stdin);
    }

    /// <summary>引数を 1 つずつ渡す（クォート不要）。</summary>
    public static Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(IEnumerable<string> args)
    {
        var psi = CreateStartInfo(DotnetHost);
        psi.ArgumentList.Add(CliAssemblyPath);
        foreach (var a in args) psi.ArgumentList.Add(a);
        return RunAsync(psi);
    }

    public static ProcessStartInfo CreateStartInfo(string fileName) => new()
    {
        FileName = fileName,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        WorkingDirectory = RepoRoot,
    };

    /// <summary>プロセスを起動し、終了と stdout/stderr の読み切りを <see cref="Timeout"/> 以内に待つ。</summary>
    public static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(ProcessStartInfo psi, byte[]? stdin = null)
    {
        psi.RedirectStandardInput = stdin is not null;
        var commandLine = psi.ArgumentList.Count > 0
            ? $"{psi.FileName} {string.Join(' ', psi.ArgumentList)}"
            : $"{psi.FileName} {psi.Arguments}";

        using var process = Process.Start(psi)!;
        // 子が stdin を読み切る前に大量に書き出してもデッドロックしないよう、先に読み取りを始める
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(Timeout);
        try
        {
            if (stdin is not null)
            {
                await process.StandardInput.BaseStream.WriteAsync(stdin, cts.Token);
                process.StandardInput.Close();
            }
            await process.WaitForExitAsync(cts.Token);
            // 終了後もパイプを握った孫プロセスが残っていると読み取りが終わらないので、ここにも上限を掛ける
            await Task.WhenAll(stdoutTask, stderrTask).WaitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            var state = process.HasExited
                ? "プロセスは終了したが stdout/stderr が閉じない（パイプを継承した子孫プロセスが残っている）"
                : "プロセスが終了しない";
            var stderr = stderrTask.IsCompletedSuccessfully ? stderrTask.Result : "(未取得)";
            throw new TimeoutException(
                $"CLI が {Timeout.TotalSeconds:0} 秒以内に完了しませんでした — {state}: {commandLine}\nstderr: {stderr}");
        }

        return (process.ExitCode, await stdoutTask, await stderrTask);
    }
}
