using BinAnalyzer.Core.Interfaces;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

/// <summary>REQ-170: imports をリゾルバ経由で解決する <c>LoadAsync</c>。</summary>
public class ImportResolverTests
{
    private readonly YamlFormatLoader _loader = new();
    private static readonly string FormatsDir = Path.Combine(AppContext.BaseDirectory, "formats");

    /// <summary>識別子→YAML の辞書で答える、ファイルシステム非依存のリゾルバ。</summary>
    private sealed class InMemoryImportResolver : IImportResolver
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
        public List<string> Requested { get; } = [];

        public InMemoryImportResolver Add(string path, string yaml)
        {
            _files[path] = yaml;
            return this;
        }

        public string Resolve(string basePath, string importPath) => ImportPath.Combine(basePath, importPath);

        public Task<string?> ReadAsync(string resolvedPath)
        {
            Requested.Add(resolvedPath);
            return Task.FromResult(_files.TryGetValue(resolvedPath, out var yaml) ? yaml : null);
        }
    }

    // ──────────────────────────────────────────────
    // 受入条件 1: Load(path) と同一の FormatDefinition
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("wav.bdef.yaml", "common/riff.bdef.yaml")]
    [InlineData("mp4.bdef.yaml", "common/isobmff.bdef.yaml")]
    public async Task LoadAsync_WithInMemoryResolver_MatchesLoadFromFile(string file, string import)
    {
        var expected = _loader.Load(Path.Combine(FormatsDir, file));

        var resolver = new InMemoryImportResolver()
            .Add("formats/" + import, File.ReadAllText(Path.Combine(FormatsDir, import)));
        var yaml = File.ReadAllText(Path.Combine(FormatsDir, file));
        var actual = await _loader.LoadAsync(yaml, "formats/" + file, resolver);

        resolver.Requested.Should().Equal("formats/" + import);
        AssertSameDefinition(actual, expected);
    }

    [Fact]
    public async Task LoadAsync_WithFileImportResolver_MatchesLoad()
    {
        var path = Path.Combine(FormatsDir, "wav.bdef.yaml");
        var expected = _loader.Load(path);

        var actual = await _loader.LoadAsync(File.ReadAllText(path), path, FileImportResolver.Instance);

        AssertSameDefinition(actual, expected);
    }

    [Fact]
    public async Task LoadAsync_FileImportResolver_AcceptsRelativeBasePath()
    {
        var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), Path.Combine(FormatsDir, "wav.bdef.yaml"));

        var actual = await _loader.LoadAsync(File.ReadAllText(relative), relative, FileImportResolver.Instance);

        actual.Structs.Should().ContainKey("raw_data");
    }

    private static void AssertSameDefinition(FormatDefinition actual, FormatDefinition expected)
    {
        actual.Name.Should().Be(expected.Name);
        actual.RootStruct.Should().Be(expected.RootStruct);
        actual.Endianness.Should().Be(expected.Endianness);
        actual.Structs.Keys.Should().BeEquivalentTo(expected.Structs.Keys);
        foreach (var (name, s) in expected.Structs)
            actual.Structs[name].Fields.Select(f => f.Name).Should().Equal(s.Fields.Select(f => f.Name), $"struct {name}");
        actual.Enums.Keys.Should().BeEquivalentTo(expected.Enums.Keys);
        actual.Flags.Keys.Should().BeEquivalentTo(expected.Flags.Keys);
    }

    // ──────────────────────────────────────────────
    // 解決規則
    // ──────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_ResolvesTransitiveImports_RelativeToEachFile()
    {
        var resolver = new InMemoryImportResolver()
            .Add("lib/a.bdef.yaml", """
                name: A
                root: a
                imports:
                  - path: sub/b.bdef.yaml
                structs:
                  a:
                    - name: x
                      type: uint8
                """)
            .Add("lib/sub/b.bdef.yaml", """
                name: B
                root: b
                imports:
                  - path: ../../c.bdef.yaml
                structs:
                  b:
                    - name: y
                      type: uint8
                """)
            .Add("c.bdef.yaml", """
                name: C
                root: c
                structs:
                  c:
                    - name: z
                      type: uint8
                """);
        var main = """
            name: Main
            root: main
            imports:
              - path: ./lib/a.bdef.yaml
            structs:
              main:
                - name: header
                  type: struct
                  struct: c
            """;

        var format = await _loader.LoadAsync(main, "main.bdef.yaml", resolver);

        format.Structs.Keys.Should().BeEquivalentTo(["main", "a", "b", "c"]);
        resolver.Requested.Should().Equal("lib/a.bdef.yaml", "lib/sub/b.bdef.yaml", "c.bdef.yaml");
    }

    // ──────────────────────────────────────────────
    // 受入条件 2: 循環インポート検出
    // ──────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_CircularImport_Throws()
    {
        var resolver = new InMemoryImportResolver()
            .Add("b.bdef.yaml", """
                name: B
                root: b
                imports:
                  - path: a.bdef.yaml
                structs:
                  b:
                    - name: y
                      type: uint8
                """);
        var a = """
            name: A
            root: a
            imports:
              - path: b.bdef.yaml
            structs:
              a:
                - name: x
                  type: uint8
            """;

        var act = () => _loader.LoadAsync(a, "a.bdef.yaml", resolver);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*循環*a.bdef.yaml*");
    }

    [Fact]
    public async Task LoadAsync_SelfImport_Throws()
    {
        var yaml = """
            name: A
            root: a
            imports:
              - path: a.bdef.yaml
            structs:
              a:
                - name: x
                  type: uint8
            """;
        var resolver = new InMemoryImportResolver().Add("a.bdef.yaml", yaml);

        var act = () => _loader.LoadAsync(yaml, "a.bdef.yaml", resolver);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*循環*");
        resolver.Requested.Should().BeEmpty("循環は読み取り前に検出される");
    }

    [Fact]
    public async Task LoadAsync_MissingImport_ThrowsFileNotFound()
    {
        var yaml = """
            name: A
            root: a
            imports:
              - path: missing.bdef.yaml
            structs:
              a:
                - name: x
                  type: uint8
            """;

        var act = () => _loader.LoadAsync(yaml, "formats/a.bdef.yaml", new InMemoryImportResolver());

        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*missing.bdef.yaml*formats/missing.bdef.yaml*");
    }

    [Fact]
    public async Task LoadAsync_WithoutImports_DoesNotTouchResolver()
    {
        var yaml = """
            name: A
            root: a
            structs:
              a:
                - name: x
                  type: uint8
            """;
        var resolver = new InMemoryImportResolver();

        var format = await _loader.LoadAsync(yaml, "a.bdef.yaml", resolver);

        format.Name.Should().Be("A");
        resolver.Requested.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────
    // ImportPath.Combine
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("formats/wav.bdef.yaml", "common/riff.bdef.yaml", "formats/common/riff.bdef.yaml")]
    [InlineData("formats/common/riff.bdef.yaml", "../base.bdef.yaml", "formats/base.bdef.yaml")]
    [InlineData("formats/x.bdef.yaml", "./y.bdef.yaml", "formats/y.bdef.yaml")]
    [InlineData("x.bdef.yaml", "y.bdef.yaml", "y.bdef.yaml")]
    [InlineData("x.bdef.yaml", "../y.bdef.yaml", "../y.bdef.yaml")]
    [InlineData("formats/x.bdef.yaml", "/abs/y.bdef.yaml", "/abs/y.bdef.yaml")]
    [InlineData("/formats/x.bdef.yaml", "common/y.bdef.yaml", "/formats/common/y.bdef.yaml")]
    [InlineData("formats\\x.bdef.yaml", "common\\y.bdef.yaml", "formats/common/y.bdef.yaml")]
    public void ImportPath_Combine(string basePath, string importPath, string expected)
    {
        ImportPath.Combine(basePath, importPath).Should().Be(expected);
    }
}
