using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BinAnalyzer.Gui.Desktop;

/// <summary>GUI を起動する前に、Photino が必要とする OS 側のランタイムが揃っているかを調べる。</summary>
/// <remarks>
/// Photino.Native は依存ライブラリが無いと dlopen / LoadLibrary の段階で分かりにくい例外を投げる。
/// ここで先に検出して、導入方法を含むメッセージを返す。判定ロジックは <see cref="IRuntimeProbe"/> 越しに OS 差分を
/// 隔離しているので、どの OS 上でも全経路をテストできる。
/// </remarks>
public static class DesktopRuntimeCheck
{
    /// <summary>Linux で Photino.Native.so が動的リンクするライブラリ（4.0.22 の <c>DT_NEEDED</c> より）。</summary>
    internal static readonly string[] LinuxLibraries =
    [
        "libwebkit2gtk-4.1.so.0",
        "libjavascriptcoregtk-4.1.so.0",
        "libgtk-3.so.0",
        "libnotify.so.4",
    ];

    /// <summary>WebView2 Runtime（Evergreen）のクライアント ID。Microsoft のドキュメント「WebView2 ランタイムの検出」に従う。</summary>
    internal const string WebView2ClientKey = @"Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

    public static RuntimeCheckResult Check() => Check(SystemRuntimeProbe.Instance);

    internal static RuntimeCheckResult Check(IRuntimeProbe probe)
    {
        if (probe.IsWindows)
            return CheckWindows(probe);
        if (probe.IsLinux)
            return CheckLinux(probe);
        // macOS は WKWebView が OS 標準。その他の OS は Photino 側に任せる
        return RuntimeCheckResult.Ok;
    }

    private static RuntimeCheckResult CheckWindows(IRuntimeProbe probe)
    {
        if (probe.GetWebView2Version() is { Length: > 0 })
            return RuntimeCheckResult.Ok;

        return RuntimeCheckResult.Missing(
            "GUI の起動に必要な Microsoft Edge WebView2 ランタイムが見つかりません。\n" +
            "  Windows 11 には標準で含まれています。Windows 10 では次から Evergreen ランタイムを導入してください:\n" +
            "  https://developer.microsoft.com/microsoft-edge/webview2/\n" +
            "  （winget: winget install Microsoft.EdgeWebView2Runtime）");
    }

    private static RuntimeCheckResult CheckLinux(IRuntimeProbe probe)
    {
        var missing = LinuxLibraries.Where(lib => !probe.CanLoadNativeLibrary(lib)).ToArray();
        if (missing.Length > 0)
        {
            return RuntimeCheckResult.Missing(
                "GUI の起動に必要な共有ライブラリが見つかりません: " + string.Join(", ", missing) + "\n" +
                "  Debian / Ubuntu: sudo apt install libwebkit2gtk-4.1-0 libgtk-3-0 libnotify4\n" +
                "  Fedora:          sudo dnf install webkit2gtk4.1 gtk3 libnotify\n" +
                "  Arch:            sudo pacman -S webkit2gtk-4.1 gtk3 libnotify");
        }

        if (!probe.HasDisplay)
        {
            return RuntimeCheckResult.Missing(
                "GUI の起動に必要なディスプレイが見つかりません（DISPLAY / WAYLAND_DISPLAY が未設定）。\n" +
                "  デスクトップセッション内で実行するか、xvfb-run 経由で起動してください。");
        }

        return RuntimeCheckResult.Ok;
    }
}

public sealed record RuntimeCheckResult(bool IsOk, string? Message)
{
    public static readonly RuntimeCheckResult Ok = new(true, null);
    public static RuntimeCheckResult Missing(string message) => new(false, message);
}

/// <summary>OS 依存の問い合わせを差し替え可能にする。</summary>
internal interface IRuntimeProbe
{
    bool IsWindows { get; }
    bool IsLinux { get; }
    /// <summary>Linux: 共有ライブラリを dlopen できるか。</summary>
    bool CanLoadNativeLibrary(string name);
    /// <summary>Linux: X11 / Wayland のディスプレイに接続できる見込みがあるか。</summary>
    bool HasDisplay { get; }
    /// <summary>Windows: WebView2 Runtime のバージョン文字列。未導入なら null。</summary>
    string? GetWebView2Version();
}

internal sealed class SystemRuntimeProbe : IRuntimeProbe
{
    public static readonly SystemRuntimeProbe Instance = new();

    public bool IsWindows => OperatingSystem.IsWindows();
    public bool IsLinux => OperatingSystem.IsLinux();

    public bool CanLoadNativeLibrary(string name)
    {
        if (!NativeLibrary.TryLoad(name, out var handle))
            return false;
        NativeLibrary.Free(handle);
        return true;
    }

    public bool HasDisplay
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
        || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

    public string? GetWebView2Version()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        // 64bit OS の全ユーザー向け → 32bit OS の全ユーザー向け → 現在ユーザー向け、の順で探す
        return ReadPv(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\" + DesktopRuntimeCheck.WebView2ClientKey)
            ?? ReadPv(Registry.LocalMachine, @"SOFTWARE\" + DesktopRuntimeCheck.WebView2ClientKey)
            ?? ReadPv(Registry.CurrentUser, @"Software\" + DesktopRuntimeCheck.WebView2ClientKey);

        static string? ReadPv(RegistryKey root, string subKey)
        {
            try
            {
                using var key = root.OpenSubKey(subKey);
                var pv = key?.GetValue("pv") as string;
                // 未導入状態では "0.0.0.0" が残ることがある
                return string.IsNullOrEmpty(pv) || pv == "0.0.0.0" ? null : pv;
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                return null;
            }
        }
    }
}
