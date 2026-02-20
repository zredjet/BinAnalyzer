using BinAnalyzer.Web.Services;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Web.Tests;

public sealed class FormatServiceTests
{
    private const string FormatListJson = """
        [
          { "name": "PNG", "file": "png.bdef.yaml", "extensions": [".png"] },
          { "name": "JPEG", "file": "jpeg.bdef.yaml", "extensions": [".jpg", ".jpeg"] },
          { "name": "ZIP", "file": "zip.bdef.yaml", "extensions": [".zip"] },
          { "name": "ELF", "file": "elf.bdef.yaml", "extensions": [".elf", ".so", ".o"] }
        ]
        """;

    private const string MinimalYaml = """
        name: Test
        endianness: big
        root: root
        structs:
          root:
            - name: magic
              type: u8
        """;

    private static (FormatService service, StubHttpHandler handler) CreateService()
    {
        var handler = new StubHttpHandler();
        handler.Register("formats/format-list.json", FormatListJson);
        handler.Register("formats/png.bdef.yaml", MinimalYaml);

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var service = new FormatService(http);
        return (service, handler);
    }

    [Fact]
    public async Task GetFormatListAsync_ReturnsAllFormats()
    {
        var (service, _) = CreateService();

        var list = await service.GetFormatListAsync();

        list.Should().HaveCount(4);
        list.Select(f => f.Name).Should().Contain(["PNG", "JPEG", "ZIP", "ELF"]);
    }

    [Fact]
    public async Task GetFormatListAsync_CachesResult()
    {
        var (service, handler) = CreateService();

        await service.GetFormatListAsync();
        await service.GetFormatListAsync();

        handler.RequestedUrls
            .Count(u => u == "formats/format-list.json")
            .Should().Be(1, "format list should be fetched only once");
    }

    [Fact]
    public async Task DetectFormat_MatchesExtension()
    {
        var (service, _) = CreateService();

        var result = await service.DetectFormat(".png");

        result.Should().NotBeNull();
        result!.Name.Should().Be("PNG");
        result.File.Should().Be("png.bdef.yaml");
    }

    [Fact]
    public async Task DetectFormat_CaseInsensitive()
    {
        var (service, _) = CreateService();

        var result = await service.DetectFormat(".PNG");

        result.Should().NotBeNull();
        result!.Name.Should().Be("PNG");
    }

    [Fact]
    public async Task DetectFormat_MultipleExtensions_MatchesAny()
    {
        var (service, _) = CreateService();

        var jpg = await service.DetectFormat(".jpg");
        var jpeg = await service.DetectFormat(".jpeg");

        jpg.Should().NotBeNull();
        jpeg.Should().NotBeNull();
        jpg!.File.Should().Be("jpeg.bdef.yaml");
        jpeg!.File.Should().Be("jpeg.bdef.yaml");
    }

    [Fact]
    public async Task DetectFormat_NoMatch_ReturnsNull()
    {
        var (service, _) = CreateService();

        var result = await service.DetectFormat(".xyz");

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoadFormatAsync_ReturnsFormatDefinition()
    {
        var (service, _) = CreateService();

        var format = await service.LoadFormatAsync("png.bdef.yaml");

        format.Should().NotBeNull();
        format.Name.Should().Be("Test");
        format.RootStruct.Should().Be("root");
    }

    [Fact]
    public async Task LoadFormatAsync_CachesResult()
    {
        var (service, handler) = CreateService();

        await service.LoadFormatAsync("png.bdef.yaml");
        await service.LoadFormatAsync("png.bdef.yaml");

        handler.RequestedUrls
            .Count(u => u == "formats/png.bdef.yaml")
            .Should().Be(1, "YAML should be fetched only once");
    }
}
