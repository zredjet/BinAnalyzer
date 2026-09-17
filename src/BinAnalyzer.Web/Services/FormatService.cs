using System.Net.Http.Json;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;

namespace BinAnalyzer.Web.Services;

public sealed class FormatService
{
    private readonly HttpClient _http;
    private readonly YamlFormatLoader _loader = new();
    private readonly HttpImportResolver _resolver;
    private readonly Dictionary<string, FormatDefinition> _cache = new();
    private readonly Dictionary<string, string> _yamlCache = new();
    private List<FormatEntry>? _formatList;

    public FormatService(HttpClient http)
    {
        _http = http;
        _resolver = new HttpImportResolver(http);
    }

    public async Task<List<FormatEntry>> GetFormatListAsync()
    {
        if (_formatList is not null)
            return _formatList;

        _formatList = await _http.GetFromJsonAsync<List<FormatEntry>>("formats/format-list.json")
            ?? [];
        return _formatList;
    }

    public async Task<FormatDefinition> LoadFormatAsync(string fileName)
    {
        if (_cache.TryGetValue(fileName, out var cached))
            return cached;

        var yaml = await LoadYamlAsync(fileName);
        // imports は formats/ 配下の相対パスとして HTTP で解決する（REQ-170）
        var format = await _loader.LoadAsync(yaml, FormatUrl(fileName), _resolver);
        _cache[fileName] = format;
        return format;
    }

    /// <summary>フォーマット定義の YAML テキストを取得する（GUI の定義ビュー用）。</summary>
    public async Task<string> LoadYamlAsync(string fileName)
    {
        if (_yamlCache.TryGetValue(fileName, out var cached))
            return cached;

        var yaml = await _http.GetStringAsync(FormatUrl(fileName));
        _yamlCache[fileName] = yaml;
        return yaml;
    }

    private static string FormatUrl(string fileName) => $"formats/{fileName}";

    public async Task<FormatEntry?> DetectFormat(string fileExtension)
    {
        var formats = await GetFormatListAsync();
        return formats.FirstOrDefault(f =>
            f.Extensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase));
    }
}

public sealed class FormatEntry
{
    public string Name { get; set; } = "";
    public string File { get; set; } = "";
    public List<string> Extensions { get; set; } = [];
}
