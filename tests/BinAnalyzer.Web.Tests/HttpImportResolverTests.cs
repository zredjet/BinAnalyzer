using BinAnalyzer.Web.Services;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Web.Tests;

public sealed class HttpImportResolverTests
{
    private static (HttpImportResolver resolver, StubHttpHandler handler) Create()
    {
        var handler = new StubHttpHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return (new HttpImportResolver(http), handler);
    }

    [Fact]
    public void Resolve_CombinesRelativeToImportingFile()
    {
        var (resolver, _) = Create();

        resolver.Resolve("formats/wav.bdef.yaml", "common/riff.bdef.yaml").Should().Be("formats/common/riff.bdef.yaml");
        resolver.Resolve("formats/common/riff.bdef.yaml", "../x.bdef.yaml").Should().Be("formats/x.bdef.yaml");
    }

    [Fact]
    public async Task ReadAsync_ReturnsBody()
    {
        var (resolver, handler) = Create();
        handler.Register("formats/common/riff.bdef.yaml", "name: RIFF");

        var text = await resolver.ReadAsync("formats/common/riff.bdef.yaml");

        text.Should().Be("name: RIFF");
        handler.RequestedUrls.Should().Equal("formats/common/riff.bdef.yaml");
    }

    [Fact]
    public async Task ReadAsync_NotFound_ReturnsNull()
    {
        var (resolver, _) = Create();

        var text = await resolver.ReadAsync("formats/common/missing.bdef.yaml");

        text.Should().BeNull();
    }
}
