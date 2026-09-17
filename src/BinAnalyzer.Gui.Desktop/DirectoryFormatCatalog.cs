using System.Text.Json;
using BinAnalyzer.Dsl;
using BinAnalyzer.Gui.Abstractions;

namespace BinAnalyzer.Gui.Desktop;

/// <summary>
/// デスクトップ用カタログ。<c>formats/</c> ディレクトリ（exe の隣、またはカレント）と、
/// CLI で <c>-f</c> 指定されたファイルを提供する。<c>YamlFormatLoader.Load(path)</c> を使うので imports も解決される。
/// </summary>
public sealed class DirectoryFormatCatalog : IFormatCatalog
{
    private readonly YamlFormatLoader _loader = new();
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FormatCatalogEntry> _entries = [];
    private readonly Dictionary<string, FormatDocument> _cache = new(StringComparer.OrdinalIgnoreCase);

    public DirectoryFormatCatalog(IEnumerable<string> searchDirectories, string? explicitFormatPath = null)
    {
        foreach (var dir in searchDirectories.Where(Directory.Exists))
            AddDirectory(dir);
        if (explicitFormatPath is not null && File.Exists(explicitFormatPath))
            AddFile(Path.GetFullPath(explicitFormatPath), prepend: true);
    }

    /// <summary>既定の探索先: exe の隣の formats/、カレントの formats/、カレント。</summary>
    public static DirectoryFormatCatalog Default(string? explicitFormatPath)
        => new(
        [
            Path.Combine(AppContext.BaseDirectory, "formats"),
            Path.Combine(Directory.GetCurrentDirectory(), "formats"),
        ], explicitFormatPath);

    private void AddDirectory(string dir)
    {
        var listPath = Path.Combine(dir, "format-list.json");
        var extensions = new Dictionary<string, (string Name, string[] Ext)>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(listPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(listPath));
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    var file = e.GetProperty("file").GetString() ?? "";
                    var name = e.GetProperty("name").GetString() ?? file;
                    var ext = e.TryGetProperty("extensions", out var exts)
                        ? exts.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToArray()
                        : [];
                    extensions[file] = (name, ext);
                }
            }
            catch (JsonException) { }
        }
        foreach (var path in Directory.EnumerateFiles(dir, "*.bdef.yaml").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var file = Path.GetFileName(path);
            if (_files.ContainsKey(file)) continue;
            _files[file] = path;
            var (name, ext) = extensions.TryGetValue(file, out var meta) ? meta : (NameFromFile(file), []);
            _entries.Add(new FormatCatalogEntry(name, file, ext));
        }
    }

    private void AddFile(string path, bool prepend)
    {
        var file = Path.GetFileName(path);
        if (_files.TryGetValue(file, out var existing) && string.Equals(existing, path, StringComparison.OrdinalIgnoreCase))
            return;
        _files[file] = path;
        _entries.RemoveAll(e => string.Equals(e.File, file, StringComparison.OrdinalIgnoreCase));
        var entry = new FormatCatalogEntry(NameFromFile(file), file, []);
        if (prepend) _entries.Insert(0, entry); else _entries.Add(entry);
    }

    private static string NameFromFile(string file)
        => file.EndsWith(".bdef.yaml", StringComparison.OrdinalIgnoreCase) ? file[..^".bdef.yaml".Length].ToUpperInvariant() : file;

    public Task<IReadOnlyList<FormatCatalogEntry>> ListAsync() => Task.FromResult<IReadOnlyList<FormatCatalogEntry>>(_entries);

    public Task<FormatDocument> LoadAsync(string file)
    {
        if (_cache.TryGetValue(file, out var cached))
            return Task.FromResult(cached);
        if (!_files.TryGetValue(file, out var path))
            throw new FileNotFoundException($"フォーマット定義が見つかりません: {file}");
        var definition = _loader.Load(path);
        var doc = new FormatDocument(file, definition.Name, definition, File.ReadAllText(path));
        _cache[file] = doc;
        return Task.FromResult(doc);
    }

    public Task<FormatCatalogEntry?> DetectByExtensionAsync(string extension)
        => Task.FromResult(_entries.FirstOrDefault(e => e.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)));
}
