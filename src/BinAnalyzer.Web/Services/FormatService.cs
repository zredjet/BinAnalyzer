using System.Net.Http.Json;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;

namespace BinAnalyzer.Web.Services;

public sealed class FormatService
{
    private readonly HttpClient _http;
    private readonly YamlFormatLoader _loader = new();
    private readonly Dictionary<string, FormatDefinition> _cache = new();
    private List<FormatEntry>? _formatList;

    public FormatService(HttpClient http)
    {
        _http = http;
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

        var yaml = await _http.GetStringAsync($"formats/{fileName}");
        var format = _loader.LoadFromString(yaml);
        _cache[fileName] = format;
        return format;
    }

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
