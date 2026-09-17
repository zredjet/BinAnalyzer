using BinAnalyzer.Gui.Desktop;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Gui.Tests;

public sealed class EmbeddedWebRootFileProviderTests
{
    private readonly EmbeddedWebRootFileProvider _provider = new();

    [Theory]
    [InlineData("/index.html")]
    [InlineData("index.html")]
    [InlineData("/")]
    [InlineData("")]
    public void IndexHtml_IsServedFromDesktopAssembly(string path)
    {
        var file = _provider.GetFileInfo(path);
        file.Exists.Should().BeTrue();
        file.Length.Should().BeGreaterThan(0);
        using var reader = new StreamReader(file.CreateReadStream());
        reader.ReadToEnd().Should().Contain("_framework/blazor.webview.js").And.Contain("_content/BinAnalyzer.Gui/gui.css");
    }

    [Theory]
    [InlineData("/_content/BinAnalyzer.Gui/gui.css", ".gui-root")]
    [InlineData("_content/BinAnalyzer.Gui/gui.js", "binGui")]
    public void GuiAssets_AreServedFromGuiAssembly(string path, string expectedContent)
    {
        var file = _provider.GetFileInfo(path);
        file.Exists.Should().BeTrue();
        using var reader = new StreamReader(file.CreateReadStream());
        reader.ReadToEnd().Should().Contain(expectedContent);
    }

    [Fact]
    public void UnknownPath_IsNotFound()
    {
        _provider.GetFileInfo("/nope.txt").Exists.Should().BeFalse();
        _provider.GetDirectoryContents("/").Exists.Should().BeFalse();
    }

    [Fact]
    public async Task DirectoryFormatCatalog_ListsFormatsDirectory_AndExplicitFile()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "formats");
        var catalog = new DirectoryFormatCatalog([dir], Path.Combine(dir, "png.bdef.yaml"));
        var list = await catalog.ListAsync();
        list.Select(e => e.File).Should().Contain(["png.bdef.yaml", "bmp.bdef.yaml"]);
        var doc = await catalog.LoadAsync("png.bdef.yaml");
        doc.Definition.Name.Should().Be("PNG");
        doc.YamlText.Should().Contain("structs:");
    }
}
