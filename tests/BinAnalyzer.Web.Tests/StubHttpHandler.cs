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

    private TaskCompletionSource? _gate;
    private string? _gateUrl;

    /// <summary>リクエストされた URL の履歴。キャッシュテストで使用。</summary>
    public IReadOnlyList<string> RequestedUrls => _requestedUrls;

    public void Register(string relativeUrl, string responseBody)
    {
        _responses[relativeUrl] = responseBody;
    }

    public bool HasRegistration(string relativeUrl) => _responses.ContainsKey(relativeUrl);

    /// <summary>指定URLへのリクエストをブロックするゲートを設定。Release()で解放。</summary>
    public void SetGate(string relativeUrl)
    {
        _gateUrl = relativeUrl;
        _gate = new TaskCompletionSource();
    }

    /// <summary>SetGateで設定したゲートを解放する。</summary>
    public void Release() => _gate?.TrySetResult();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        _requestedUrls.Add(path);

        if (_gate is not null && string.Equals(path, _gateUrl, StringComparison.OrdinalIgnoreCase))
        {
            await _gate.Task;
        }

        if (_responses.TryGetValue(path, out var body))
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/octet-stream"),
            };
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
