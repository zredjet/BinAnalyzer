using BinAnalyzer.Core.Models;
using BinAnalyzer.Gui.Abstractions;
using BinAnalyzer.Gui.State;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using Photino.NET;

namespace BinAnalyzer.Gui.Desktop;

/// <summary>CLI から GUI を起動するためのオプション。</summary>
public sealed record GuiLaunchOptions
{
    /// <summary>最初に開くファイル（null なら空の状態で起動）。</summary>
    public string? FilePath { get; init; }
    public byte[]? Data { get; init; }
    public string? DisplayName { get; init; }
    /// <summary><c>-f</c> で指定されたフォーマット定義ファイルのパス。</summary>
    public string? FormatPath { get; init; }
    public Endianness? EndianOverride { get; init; }
    /// <summary>テスト専用。指定時間後に窓を自動で閉じる（<see cref="GuiAutoClose"/>）。</summary>
    public TimeSpan? AutoCloseAfter { get; init; }
}

/// <summary>Photino.Blazor でデスクトップ窓を開き、閉じられるまでブロックする。メインスレッドから呼ぶこと。</summary>
public sealed class GuiApp
{
    public int Run(GuiLaunchOptions options, string[]? args = null)
    {
        PhotinoBlazorApp? app = null;
        var builder = PhotinoBlazorAppBuilder.CreateDefault(new EmbeddedWebRootFileProvider(), args ?? []);
        var catalog = DirectoryFormatCatalog.Default(options.FormatPath);
        builder.Services.AddSingleton<IFormatCatalog>(catalog);
        builder.Services.AddSingleton<IFileSource>(new PhotinoFileSource(() => app?.MainWindow));
        var session = new GuiSession(catalog, new PhotinoFileSource(() => app?.MainWindow))
        {
            LargeFileThreshold = LargeFileThresholdFromEnvironment(),
        };
        builder.Services.AddSingleton(session);
        builder.RootComponents.Add<DesktopRoot>("app");

        app = builder.Build();
        // Photino 既定のログ（Set*/SendWebMessage の全記録）は CI ログと通常利用を汚すので、デバッグ指定時のみ出す
        var debug = Environment.GetEnvironmentVariable("BINANALYZER_GUI_DEBUG") == "1";
        app.MainWindow
            .SetLogVerbosity(debug ? 2 : 0)
            .SetTitle("BinAnalyzer")
            .SetSize(1360, 880)
            .SetUseOsDefaultLocation(true)
            .Center();

        if (options.Data is not null || options.FilePath is not null)
        {
            var data = options.Data ?? File.ReadAllBytes(options.FilePath!);
            var name = options.DisplayName ?? Path.GetFileName(options.FilePath ?? "<stdin>");
            var formatFile = options.FormatPath is null ? null : Path.GetFileName(options.FormatPath);
            // Blazor のディスパッチャ外で状態を作るが、まだコンポーネントは描画されていないので安全
            session.OpenAsync(new OpenedFile(name, data, options.FilePath), formatFile, options.EndianOverride).GetAwaiter().GetResult();
        }

        if (options.AutoCloseAfter is { } delay)
        {
            // 窓が生成された後にタイマーを起動する。Close は UI スレッドから呼ぶ必要があるので Invoke 越しに渡す
            var window = app.MainWindow;
            window.RegisterWindowCreatedHandler((_, _) =>
            {
                _ = Task.Delay(delay).ContinueWith(_ => window.Invoke(window.Close), TaskScheduler.Default);
            });
        }

        app.Run();
        return 0;
    }

    /// <summary>
    /// 大きなファイルを開く前に確認する閾値。既定 256 MB。環境変数 <c>BINANALYZER_GUI_LARGE_FILE_MB</c> で変更でき、0 で確認しない。
    /// </summary>
    public static long? LargeFileThresholdFromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable("BINANALYZER_GUI_LARGE_FILE_MB");
        if (string.IsNullOrWhiteSpace(raw))
            return GuiSession.DefaultLargeFileThreshold;
        if (!long.TryParse(raw, out var mb) || mb < 0)
            return GuiSession.DefaultLargeFileThreshold;
        return mb == 0 ? null : mb * 1024 * 1024;
    }
}
