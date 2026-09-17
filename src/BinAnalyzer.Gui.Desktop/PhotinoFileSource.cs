using BinAnalyzer.Gui.Abstractions;
using Photino.NET;

namespace BinAnalyzer.Gui.Desktop;

/// <summary>Photino のネイティブ「開く」ダイアログ。同期版は Photino.Blazor 下でクラッシュするため非同期版を使う。</summary>
public sealed class PhotinoFileSource : IFileSource
{
    private readonly Func<PhotinoWindow?> _window;

    public PhotinoFileSource(Func<PhotinoWindow?> window)
    {
        _window = window;
    }

    public bool SupportsNativePicker => true;

    public async Task<OpenedFile?> PickAsync()
    {
        var window = _window();
        if (window is null) return null;
        var paths = await window.ShowOpenFileAsync("バイナリファイルを開く", multiSelect: false);
        var path = paths?.FirstOrDefault();
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        var data = await File.ReadAllBytesAsync(path);
        return new OpenedFile(Path.GetFileName(path), data, path);
    }
}
