using BinAnalyzer.Gui.Desktop;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Gui.Tests;

/// <summary>REQ-176 受入条件 2: WebView2 / WebKitGTK が無い環境で分かりやすいエラーになること。</summary>
public class DesktopRuntimeCheckTests
{
    private sealed class FakeProbe : IRuntimeProbe
    {
        public bool IsWindows { get; init; }
        public bool IsLinux { get; init; }
        public HashSet<string> Libraries { get; init; } = [];
        public bool HasDisplay { get; init; } = true;
        public string? WebView2Version { get; init; }

        public bool CanLoadNativeLibrary(string name) => Libraries.Contains(name);
        public string? GetWebView2Version() => WebView2Version;
    }

    private static FakeProbe LinuxWithAllLibraries() => new()
    {
        IsLinux = true,
        Libraries = [.. DesktopRuntimeCheck.LinuxLibraries],
    };

    [Fact]
    public void MacOS_IsAlwaysOk()
    {
        DesktopRuntimeCheck.Check(new FakeProbe()).IsOk.Should().BeTrue();
    }

    [Fact]
    public void Windows_WithWebView2_IsOk()
    {
        var result = DesktopRuntimeCheck.Check(new FakeProbe { IsWindows = true, WebView2Version = "128.0.2739.42" });
        result.IsOk.Should().BeTrue();
        result.Message.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Windows_WithoutWebView2_ExplainsHowToInstall(string? version)
    {
        var result = DesktopRuntimeCheck.Check(new FakeProbe { IsWindows = true, WebView2Version = version });
        result.IsOk.Should().BeFalse();
        result.Message.Should().Contain("WebView2").And.Contain("https://developer.microsoft.com/microsoft-edge/webview2/");
    }

    [Fact]
    public void Linux_WithAllLibrariesAndDisplay_IsOk()
    {
        DesktopRuntimeCheck.Check(LinuxWithAllLibraries()).IsOk.Should().BeTrue();
    }

    [Fact]
    public void Linux_MissingWebKit_ListsMissingLibrariesAndAptPackage()
    {
        var probe = LinuxWithAllLibraries();
        probe.Libraries.Remove("libwebkit2gtk-4.1.so.0");
        probe.Libraries.Remove("libjavascriptcoregtk-4.1.so.0");

        var result = DesktopRuntimeCheck.Check(probe);

        result.IsOk.Should().BeFalse();
        result.Message.Should().Contain("libwebkit2gtk-4.1.so.0")
            .And.Contain("libjavascriptcoregtk-4.1.so.0")
            .And.NotContain("libgtk-3.so.0", "揃っているライブラリは列挙しない")
            .And.Contain("apt install libwebkit2gtk-4.1-0");
    }

    [Fact]
    public void Linux_WithoutDisplay_ExplainsXvfb()
    {
        var probe = new FakeProbe { IsLinux = true, Libraries = [.. DesktopRuntimeCheck.LinuxLibraries], HasDisplay = false };
        var result = DesktopRuntimeCheck.Check(probe);
        result.IsOk.Should().BeFalse();
        result.Message.Should().Contain("DISPLAY").And.Contain("xvfb-run");
    }

    [Fact]
    public void Linux_MissingLibraryIsReportedBeforeDisplay()
    {
        var probe = new FakeProbe { IsLinux = true, HasDisplay = false };
        DesktopRuntimeCheck.Check(probe).Message.Should().Contain("共有ライブラリ");
    }

    [Fact]
    public void SystemProbe_DoesNotThrowOnCurrentOs()
    {
        // 実 OS でクラッシュしないことだけ確認する（結果は環境依存）
        var act = () => DesktopRuntimeCheck.Check();
        act.Should().NotThrow();
    }
}
