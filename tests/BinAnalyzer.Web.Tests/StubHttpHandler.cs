using System.Net;
using System.Text;

namespace BinAnalyzer.Web.Tests;

/// <summary>
/// テスト用の HttpMessageHandler。URL→レスポンス本文のマッピングを登録して使用。
/// </summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, string> _responses = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _requestedUrls = [];

    /// <summary>リクエストされた URL の履歴。キャッシュテストで使用。</summary>
    public IReadOnlyList<string> RequestedUrls => _requestedUrls;

    public void Register(string relativeUrl, string responseBody)
    {
        _responses[relativeUrl] = responseBody;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        _requestedUrls.Add(path);

        if (_responses.TryGetValue(path, out var body))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/octet-stream"),
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
