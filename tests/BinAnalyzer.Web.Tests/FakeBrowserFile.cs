using Microsoft.AspNetCore.Components.Forms;

namespace BinAnalyzer.Web.Tests;

/// <summary>
/// テスト用の IBrowserFile スタブ。Size を任意の値に設定可能。
/// D4テスト（100MB超ファイル）用。
/// </summary>
internal sealed class FakeBrowserFile : IBrowserFile
{
    public string Name { get; init; } = "file.bin";
    public DateTimeOffset LastModified { get; init; } = DateTimeOffset.UtcNow;
    public long Size { get; init; }
    public string ContentType { get; init; } = "application/octet-stream";

    public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Should not be called for oversized files");
}
