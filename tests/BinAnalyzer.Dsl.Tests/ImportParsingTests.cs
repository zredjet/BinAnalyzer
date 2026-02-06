using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

public class ImportParsingTests
{
    private readonly YamlFormatLoader _loader = new();

    [Fact]
    public void LoadFromString_WithImports_NoBasePath_Throws()
    {
        var yaml = """
            name: Test
            root: main
            imports:
              - path: common.bdef.yaml
            structs:
              main:
                - name: x
                  type: uint8
            """;

        var act = () => _loader.LoadFromString(yaml);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*imports*");
    }

    [Fact]
    public void Load_WithImports_MergesStructs()
    {
        using var tempDir = new TempDirectory();
        var commonYaml = """
            name: Common
            root: common_header
            structs:
              common_header:
                - name: magic
                  type: uint32
            """;
        var mainYaml = """
            name: Main
            root: main
            imports:
              - path: common.bdef.yaml
            structs:
              main:
                - name: header
                  type: struct
                  struct: common_header
            """;

        File.WriteAllText(Path.Combine(tempDir.Path, "common.bdef.yaml"), commonYaml);
        File.WriteAllText(Path.Combine(tempDir.Path, "main.bdef.yaml"), mainYaml);

        var format = _loader.Load(Path.Combine(tempDir.Path, "main.bdef.yaml"));

        format.Name.Should().Be("Main");
        format.Structs.Should().ContainKey("main");
        format.Structs.Should().ContainKey("common_header");
        format.Structs["common_header"].Fields[0].Name.Should().Be("magic");
    }

    [Fact]
    public void Load_WithImports_MergesEnums()
    {
        using var tempDir = new TempDirectory();
        var enumsYaml = """
            name: Enums
            root: dummy
            enums:
              color_type:
                - value: 0
                  label: grayscale
                - value: 2
                  label: truecolor
            structs:
              dummy:
                - name: x
                  type: uint8
            """;
        var mainYaml = """
            name: Main
            root: main
            imports:
              - path: enums.bdef.yaml
            structs:
              main:
                - name: ct
                  type: uint8
                  enum: color_type
            """;

        File.WriteAllText(Path.Combine(tempDir.Path, "enums.bdef.yaml"), enumsYaml);
        File.WriteAllText(Path.Combine(tempDir.Path, "main.bdef.yaml"), mainYaml);

        var format = _loader.Load(Path.Combine(tempDir.Path, "main.bdef.yaml"));

        format.Enums.Should().ContainKey("color_type");
        format.Enums["color_type"].Entries.Should().HaveCount(2);
    }

    [Fact]
    public void Load_WithImports_MergesFlags()
    {
        using var tempDir = new TempDirectory();
        var flagsYaml = """
            name: FlagsLib
            root: dummy
            flags:
              chunk_flags:
                bit_size: 32
                fields:
                  - name: ancillary
                    bit: 5
            structs:
              dummy:
                - name: x
                  type: uint8
            """;
        var mainYaml = """
            name: Main
            root: main
            imports:
              - path: flags.bdef.yaml
            structs:
              main:
                - name: x
                  type: uint8
            """;

        File.WriteAllText(Path.Combine(tempDir.Path, "flags.bdef.yaml"), flagsYaml);
        File.WriteAllText(Path.Combine(tempDir.Path, "main.bdef.yaml"), mainYaml);

        var format = _loader.Load(Path.Combine(tempDir.Path, "main.bdef.yaml"));

        format.Flags.Should().ContainKey("chunk_flags");
    }

    [Fact]
    public void Load_CircularImport_Throws()
    {
        using var tempDir = new TempDirectory();
        var aYaml = """
            name: A
            root: a
            imports:
              - path: b.bdef.yaml
            structs:
              a:
                - name: x
                  type: uint8
            """;
        var bYaml = """
            name: B
            root: b
            imports:
              - path: a.bdef.yaml
            structs:
              b:
                - name: y
                  type: uint8
            """;

        File.WriteAllText(Path.Combine(tempDir.Path, "a.bdef.yaml"), aYaml);
        File.WriteAllText(Path.Combine(tempDir.Path, "b.bdef.yaml"), bYaml);

        var act = () => _loader.Load(Path.Combine(tempDir.Path, "a.bdef.yaml"));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*循環*");
    }

    [Fact]
    public void Load_DuplicateStructName_Throws()
    {
        using var tempDir = new TempDirectory();
        var commonYaml = """
            name: Common
            root: shared
            structs:
              shared:
                - name: x
                  type: uint8
            """;
        var mainYaml = """
            name: Main
            root: main
            imports:
              - path: common.bdef.yaml
            structs:
              main:
                - name: x
                  type: uint8
              shared:
                - name: y
                  type: uint16
            """;

        File.WriteAllText(Path.Combine(tempDir.Path, "common.bdef.yaml"), commonYaml);
        File.WriteAllText(Path.Combine(tempDir.Path, "main.bdef.yaml"), mainYaml);

        var act = () => _loader.Load(Path.Combine(tempDir.Path, "main.bdef.yaml"));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*重複*");
    }

    [Fact]
    public void Load_MissingImportFile_Throws()
    {
        using var tempDir = new TempDirectory();
        var mainYaml = """
            name: Main
            root: main
            imports:
              - path: nonexistent.bdef.yaml
            structs:
              main:
                - name: x
                  type: uint8
            """;

        File.WriteAllText(Path.Combine(tempDir.Path, "main.bdef.yaml"), mainYaml);

        var act = () => _loader.Load(Path.Combine(tempDir.Path, "main.bdef.yaml"));
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Load_TransitiveImports_MergesAll()
    {
        using var tempDir = new TempDirectory();
        var baseYaml = """
            name: Base
            root: base_struct
            structs:
              base_struct:
                - name: magic
                  type: uint32
            """;
        var midYaml = """
            name: Mid
            root: mid_struct
            imports:
              - path: base.bdef.yaml
            structs:
              mid_struct:
                - name: header
                  type: struct
                  struct: base_struct
            """;
        var topYaml = """
            name: Top
            root: top_struct
            imports:
              - path: mid.bdef.yaml
            structs:
              top_struct:
                - name: mid
                  type: struct
                  struct: mid_struct
            """;

        File.WriteAllText(Path.Combine(tempDir.Path, "base.bdef.yaml"), baseYaml);
        File.WriteAllText(Path.Combine(tempDir.Path, "mid.bdef.yaml"), midYaml);
        File.WriteAllText(Path.Combine(tempDir.Path, "top.bdef.yaml"), topYaml);

        var format = _loader.Load(Path.Combine(tempDir.Path, "top.bdef.yaml"));

        format.Structs.Should().ContainKey("top_struct");
        format.Structs.Should().ContainKey("mid_struct");
        format.Structs.Should().ContainKey("base_struct");
    }

    [Fact]
    public void Load_NoImports_WorksNormally()
    {
        using var tempDir = new TempDirectory();
        var yaml = """
            name: Simple
            root: main
            structs:
              main:
                - name: x
                  type: uint8
            """;

        File.WriteAllText(Path.Combine(tempDir.Path, "simple.bdef.yaml"), yaml);

        var format = _loader.Load(Path.Combine(tempDir.Path, "simple.bdef.yaml"));

        format.Name.Should().Be("Simple");
        format.Structs.Should().ContainKey("main");
    }

    [Fact]
    public void LoadFromString_WithBasePath_ResolvesImports()
    {
        using var tempDir = new TempDirectory();
        var commonYaml = """
            name: Common
            root: common_header
            structs:
              common_header:
                - name: sig
                  type: bytes
                  size: "4"
            """;
        File.WriteAllText(Path.Combine(tempDir.Path, "common.bdef.yaml"), commonYaml);

        var mainYaml = """
            name: Main
            root: main
            imports:
              - path: common.bdef.yaml
            structs:
              main:
                - name: header
                  type: struct
                  struct: common_header
            """;

        var format = _loader.LoadFromString(mainYaml, Path.Combine(tempDir.Path, "main.bdef.yaml"));

        format.Structs.Should().ContainKey("main");
        format.Structs.Should().ContainKey("common_header");
    }

    /// <summary>
    /// Disposable temp directory helper for file-based tests.
    /// </summary>
    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; }

        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "binanalyzer_test_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, true); }
            catch { /* best effort cleanup */ }
        }
    }
}
