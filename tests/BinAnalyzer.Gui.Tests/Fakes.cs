using BinAnalyzer.Dsl;
using BinAnalyzer.Gui.Abstractions;
using BinAnalyzer.Gui.State;

namespace BinAnalyzer.Gui.Tests;

internal sealed class FakeFormatCatalog : IFormatCatalog
{
    private readonly Dictionary<string, (string Name, string[] Ext, string Yaml)> _formats = new();
    private readonly YamlFormatLoader _loader = new();

    public FakeFormatCatalog Add(string file, string name, string[] extensions, string yaml)
    {
        _formats[file] = (name, extensions, yaml);
        return this;
    }

    public static FakeFormatCatalog WithPngAndBmp()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "formats");
        return new FakeFormatCatalog()
            .Add("png.bdef.yaml", "PNG", [".png"], File.ReadAllText(Path.Combine(dir, "png.bdef.yaml")))
            .Add("bmp.bdef.yaml", "BMP", [".bmp"], File.ReadAllText(Path.Combine(dir, "bmp.bdef.yaml")));
    }

    public Task<IReadOnlyList<FormatCatalogEntry>> ListAsync()
        => Task.FromResult<IReadOnlyList<FormatCatalogEntry>>(_formats.Select(kv => new FormatCatalogEntry(kv.Value.Name, kv.Key, kv.Value.Ext)).ToList());

    public Task<FormatDocument> LoadAsync(string file)
    {
        var (name, _, yaml) = _formats[file];
        return Task.FromResult(new FormatDocument(file, name, _loader.LoadFromString(yaml), yaml));
    }

    public Task<FormatCatalogEntry?> DetectByExtensionAsync(string extension)
    {
        var hit = _formats.FirstOrDefault(kv => kv.Value.Ext.Contains(extension, StringComparer.OrdinalIgnoreCase));
        return Task.FromResult(hit.Key is null ? null : new FormatCatalogEntry(hit.Value.Name, hit.Key, hit.Value.Ext));
    }
}

internal sealed class FakeFileSource : IFileSource
{
    public Queue<OpenedFile?> Files { get; } = new();
    public bool SupportsNativePicker { get; init; } = true;
    public Task<OpenedFile?> PickAsync() => Task.FromResult(Files.Count > 0 ? Files.Dequeue() : null);
}

/// <summary>1x1 の最小 PNG（signature + IHDR + IDAT + IEND）。</summary>
internal static class TestPng
{
    public static byte[] Bytes(byte width = 1)
    {
        var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(ms, "IHDR", [0, 0, 0, width, 0, 0, 0, 1, 8, 2, 0, 0, 0]);
        WriteChunk(ms, "IDAT", [0x78, 0x9C, 0x63, 0x60, 0x60, 0x60, 0x00, 0x00, 0x00, 0x04, 0x00, 0x01]);
        WriteChunk(ms, "IEND", []);
        return ms.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var len = data.Length;
        s.Write([(byte)(len >> 24), (byte)(len >> 16), (byte)(len >> 8), (byte)len]);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        var crc = Crc32(typeBytes.Concat(data).ToArray());
        s.Write([(byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc]);
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return ~crc;
    }
}

internal static class SessionFactory
{
    public static async Task<(GuiSession Session, GuiDocument Doc)> WithPngAsync(byte width = 1, string name = "image.png")
    {
        var session = new GuiSession(FakeFormatCatalog.WithPngAndBmp(), new FakeFileSource());
        var doc = await session.OpenAsync(new OpenedFile(name, TestPng.Bytes(width)));
        return (session, doc!);
    }
}
