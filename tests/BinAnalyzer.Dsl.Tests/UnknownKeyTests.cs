using BinAnalyzer.Core.Interfaces;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

/// <summary>DSL の未知キーの収集（REQ-184）。</summary>
public class UnknownKeyTests
{
    private readonly YamlFormatLoader _loader = new();

    [Fact]
    public void Typos_AreCollectedWithLineAndSuggestion()
    {
        var yaml = """
            name: t
            root: s
            structs:
              s:
                fields:
                  - name: magic
                    type: bytes
                    size: "4"
                    expect: [0x4D]
                    repeat_cout: 3
            """;

        var keys = _loader.LoadFromString(yaml).UnknownKeys;

        keys.Should().BeEquivalentTo(new[]
        {
            new DslUnknownKey("expect", "struct 's' のフィールド 'magic'", "s", "magic", "expected", null, 9),
            new DslUnknownKey("repeat_cout", "struct 's' のフィールド 'magic'", "s", "magic", "repeat_count", null, 10),
        }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void KnownKeysOnly_NoUnknownKeys()
    {
        var yaml = """
            name: t
            endianness: little
            root: s
            enums:
              kind:
                - value: 0
                  label: zero
                  description: "d"
            flags:
              f:
                bit_size: 8
                fields:
                  - name: a
                    bit: 0
                    set: "yes"
                    clear: "no"
            structs:
              s:
                endianness: big
                resync_marker: [0x4D]
                fields:
                  - name: k
                    type: uint8
                    enum: kind
                  - name: sw
                    type: switch
                    switch_on: "{k}"
                    cases:
                      "0": s2
                    default: s2
                  - name: crc
                    type: uint32
                    checksum:
                      algorithm: crc32
                      range: { offset: "0", size: "1" }
                      exclude_self: true
              s2:
                - name: b
                  type: bitfield
                  size: "1"
                  fields:
                    - name: lo
                      bits: "3:0"
                      description: "low"
            """;

        _loader.LoadFromString(yaml).UnknownKeys.Should().BeEmpty();
    }

    [Theory]
    [InlineData("top", "トップレベル", null, null)]
    [InlineData("enum_entry", "enum 'kind' の 1 番目のエントリ", null, null)]
    [InlineData("flags_def", "flags 'f'", null, null)]
    [InlineData("flag_field", "flags 'f' のフィールド 'a'", null, null)]
    [InlineData("struct_def", "struct 's'", "s", null)]
    [InlineData("field", "struct 's' のフィールド 'crc'", "s", "crc")]
    [InlineData("checksum", "struct 's' のフィールド 'crc' の checksum", "s", "crc")]
    [InlineData("range", "struct 's' のフィールド 'crc' の checksum の range", "s", "crc")]
    [InlineData("ranges_item", "struct 's' のフィールド 'crc2' の checksum の 1 番目の範囲", "s", "crc2")]
    [InlineData("bitfield_entry", "struct 's2' のフィールド 'b' の bitfield エントリ 'lo'", "s2", "b")]
    [InlineData("legacy_field", "struct 's2' のフィールド 'b'", "s2", "b")]
    public void UnknownKey_IsCollectedAtEveryLevel(string level, string context, string? structName, string? fieldName)
    {
        string X(string at) => at == level ? $"\n{new string(' ', Indent(at))}zz_{level}: 1" : "";
        var yaml = $$"""
            name: t{{X("top")}}
            root: s
            enums:
              kind:
                - value: 0
                  label: zero{{X("enum_entry")}}
            flags:
              f:
                bit_size: 8{{X("flags_def")}}
                fields:
                  - name: a
                    bit: 0{{X("flag_field")}}
            structs:
              s:
                endianness: big{{X("struct_def")}}
                fields:
                  - name: crc
                    type: uint32{{X("field")}}
                    checksum:
                      algorithm: crc32{{X("checksum")}}
                      range:
                        offset: "0"
                        size: "1"{{X("range")}}
                  - name: crc2
                    type: uint32
                    checksum:
                      algorithm: crc32
                      ranges:
                        - offset: "0"
                          size: "1"{{X("ranges_item")}}
              s2:
                - name: b
                  type: bitfield
                  size: "1"{{X("legacy_field")}}
                  fields:
                    - name: lo
                      bits: "3:0"{{X("bitfield_entry")}}
            """;

        var keys = _loader.LoadFromString(yaml).UnknownKeys;

        var key = keys.Should().ContainSingle().Subject;
        key.Key.Should().Be($"zz_{level}");
        key.Context.Should().Be(context);
        key.StructName.Should().Be(structName);
        key.FieldName.Should().Be(fieldName);
        yaml.Split('\n')[key.SourceLine!.Value - 1].Trim().Should().Be($"zz_{level}: 1");
    }

    private static int Indent(string level) => level switch
    {
        "top" => 0,
        "enum_entry" => 6,
        "flags_def" => 4,
        "flag_field" => 8,
        "struct_def" => 4,
        "field" => 8,
        "checksum" => 10,
        "range" => 12,
        "ranges_item" => 14,
        "legacy_field" => 6,
        "bitfield_entry" => 10,
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    [Fact]
    public void UserNamedKeys_AreNotChecked()
    {
        // struct 名・enum 名・flags 名・switch の case 値は利用者の名前
        var yaml = """
            name: t
            root: whatever_struct
            enums:
              any_enum_name:
                - value: 0
                  label: z
            flags:
              any_flags_name:
                bit_size: 8
                fields: []
            structs:
              whatever_struct:
                - name: k
                  type: uint8
                - name: sw
                  type: switch
                  switch_on: "{k}"
                  cases:
                    "0": whatever_struct2
                    "1": whatever_struct2
              whatever_struct2:
                - name: x
                  type: uint8
            """;

        _loader.LoadFromString(yaml).UnknownKeys.Should().BeEmpty();
    }

    [Fact]
    public void FarFromAnyKnownKey_NoSuggestion()
    {
        var yaml = """
            name: t
            root: s
            structs:
              s:
                - name: x
                  type: uint8
                  completely_unrelated: 1
            """;

        _loader.LoadFromString(yaml).UnknownKeys.Should().ContainSingle()
            .Which.Suggestion.Should().BeNull();
    }

    [Fact]
    public void Suggestion_IsCaseInsensitive()
    {
        var yaml = """
            name: t
            root: s
            structs:
              s:
                - name: x
                  type: uint8
                  Enum: kind
            """;

        _loader.LoadFromString(yaml).UnknownKeys.Should().ContainSingle()
            .Which.Suggestion.Should().Be("enum");
    }

    [Fact]
    public async Task ImportedFile_UnknownKeys_ReportImportedFileAndLine()
    {
        var resolver = new InMemoryResolver(new()
        {
            ["common.bdef.yaml"] = """
                name: Common
                root: header
                structs:
                  header:
                    - name: magic
                      type: uint32
                      expeted: [0x01]
                """,
        });
        var main = """
            name: Main
            root: main
            imports:
              - path: common.bdef.yaml
                as: c
            structs:
              main:
                - name: h
                  type: struct
                  struct: header
            """;

        var format = await _loader.LoadAsync(main, "main.bdef.yaml", resolver);

        format.UnknownKeys.Should().BeEquivalentTo(new[]
        {
            new DslUnknownKey("as", "1 番目のインポート", null, null, null, "main.bdef.yaml", 5),
            new DslUnknownKey("expeted", "struct 'header' のフィールド 'magic'", "header", "magic", "expected", "common.bdef.yaml", 7),
        });
    }

    [Fact]
    public void UnknownKeys_DoNotChangeTheDecodedDefinition()
    {
        var clean = """
            name: t
            root: s
            structs:
              s:
                - name: x
                  type: uint8
            """;
        var withTypo = clean + "\n      sizee: \"2\"";

        var a = _loader.LoadFromString(clean);
        var b = _loader.LoadFromString(withTypo);

        b.UnknownKeys.Should().ContainSingle();
        b.Structs["s"].Fields.Should().BeEquivalentTo(a.Structs["s"].Fields);
    }

    private sealed class InMemoryResolver(Dictionary<string, string> files) : IImportResolver
    {
        public string Resolve(string basePath, string importPath) => importPath;

        public Task<string?> ReadAsync(string resolvedPath) =>
            Task.FromResult(files.TryGetValue(resolvedPath, out var yaml) ? yaml : null);
    }
}
