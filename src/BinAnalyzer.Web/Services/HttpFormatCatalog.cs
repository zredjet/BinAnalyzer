using BinAnalyzer.Gui.Abstractions;

namespace BinAnalyzer.Web.Services;

/// <summary>Web(WASM) ホスト用: 既存の <see cref="FormatService"/>（HTTP 経由）をカタログとして公開する。</summary>
public sealed class HttpFormatCatalog : IFormatCatalog
{
    private readonly FormatService _service;

    public HttpFormatCatalog(FormatService service)
    {
        _service = service;
    }

    public async Task<IReadOnlyList<FormatCatalogEntry>> ListAsync()
    {
        var list = await _service.GetFormatListAsync();
        return list.Select(f => new FormatCatalogEntry(f.Name, f.File, f.Extensions)).ToList();
    }

    public async Task<FormatDocument> LoadAsync(string file)
    {
        var yaml = await _service.LoadYamlAsync(file);
        var definition = await _service.LoadFormatAsync(file);
        return new FormatDocument(file, definition.Name, definition, yaml);
    }

    public async Task<FormatCatalogEntry?> DetectByExtensionAsync(string extension)
    {
        var entry = await _service.DetectFormat(extension);
        return entry is null ? null : new FormatCatalogEntry(entry.Name, entry.File, entry.Extensions);
    }
}

/// <summary>Web ホストにはネイティブダイアログが無い。UI 側の InputFile / ドロップを使う。</summary>
public sealed class BrowserFileSource : IFileSource
{
    public bool SupportsNativePicker => false;
    public Task<OpenedFile?> PickAsync() => Task.FromResult<OpenedFile?>(null);
}
