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

    public async Task<FormatDocument> LoadAsync(string file)
    {
        var (name, _, yaml) = _formats[file];
        // imports を含む定義は、AddSource で登録した YAML をインポート先として解決する（REQ-172 の定義ビュー用）
        var definition = yaml.Contains("imports:", StringComparison.Ordinal)
            ? await _loader.LoadAsync(yaml, file, new InMemoryResolver(this))
            : _loader.LoadFromString(yaml);
        return new FormatDocument(file, name, definition, yaml);
    }

    private sealed class InMemoryResolver(FakeFormatCatalog catalog) : BinAnalyzer.Core.Interfaces.IImportResolver
    {
        public string Resolve(string basePath, string importPath) => ImportPath.Combine(basePath, importPath);
        public Task<string?> ReadAsync(string resolvedPath)
            => Task.FromResult(catalog._sources.TryGetValue(resolvedPath, out var yaml) ? yaml : null);
    }

    public Task<FormatCatalogEntry?> DetectByExtensionAsync(string extension)
    {
        var hit = _formats.FirstOrDefault(kv => kv.Value.Ext.Contains(extension, StringComparer.OrdinalIgnoreCase));
        return Task.FromResult(hit.Key is null ? null : new FormatCatalogEntry(hit.Value.Name, hit.Key, hit.Value.Ext));
    }

    /// <summary>インポート元として登録した YAML（<see cref="AddSource"/>）も返す。</summary>
    private readonly Dictionary<string, string> _sources = new(StringComparer.Ordinal);
    public List<string> SourceReads { get; } = [];

    public FakeFormatCatalog AddSource(string sourceFile, string yaml)
    {
        _sources[sourceFile] = yaml;
        return this;
    }

    public Task<string?> ReadSourceAsync(string sourceFile)
    {
        SourceReads.Add(sourceFile);
        if (_sources.TryGetValue(sourceFile, out var yaml)) return Task.FromResult<string?>(yaml);
        return Task.FromResult(_formats.TryGetValue(sourceFile, out var f) ? f.Yaml : null);
    }
}

internal sealed class FakeFileSource : IFileSource
{
    public Queue<OpenedFile?> Files { get; } = new();
    public bool SupportsNativePicker { get; init; } = true;
    public Task<OpenedFile?> PickAsync() => Task.FromResult(Files.Count > 0 ? Files.Dequeue() : null);

    /// <summary>保存要求の記録（ファイル・データ・保存先選択の有無）。</summary>
    public List<(OpenedFile File, byte[] Data, bool ChooseLocation)> Saved { get; } = [];
    /// <summary>「名前を付けて保存」で返すパス。null ならキャンセル扱い。</summary>
    public string? SaveAsPath { get; set; } = "/tmp/saved.bin";
    public bool CancelSave { get; set; }

    public Task<OpenedFile?> SaveAsync(OpenedFile file, byte[] data, bool chooseLocation)
    {
        Saved.Add((file, data, chooseLocation));
        if (CancelSave) return Task.FromResult<OpenedFile?>(null);
        var path = chooseLocation ? SaveAsPath : file.FullPath;
        if (chooseLocation && path is null) return Task.FromResult<OpenedFile?>(null);
        return Task.FromResult<OpenedFile?>(new OpenedFile(path is null ? file.Name : Path.GetFileName(path), data, path));
    }
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
