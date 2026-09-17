using System.Net;
using BinAnalyzer.Core.Interfaces;
using BinAnalyzer.Dsl;

namespace BinAnalyzer.Web.Services;

/// <summary>
/// Web(WASM) 用のインポートリゾルバ。インポート元の相対 URL（例: <c>formats/wav.bdef.yaml</c>）を基準に
/// 相対パスを字句的に結合し、<see cref="HttpClient"/> で取得する。404 は「見つからない」として <c>null</c> を返す。
/// </summary>
public sealed class HttpImportResolver : IImportResolver
{
    private readonly HttpClient _http;

    public HttpImportResolver(HttpClient http)
    {
        _http = http;
    }

    public string Resolve(string basePath, string importPath) => ImportPath.Combine(basePath, importPath);

    public async Task<string?> ReadAsync(string resolvedPath)
    {
        using var response = await _http.GetAsync(resolvedPath);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
