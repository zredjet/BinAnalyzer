using System.Text.Json.Nodes;
using BinAnalyzer.Dsl;
using BinAnalyzer.Dsl.YamlModels;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

/// <summary>DSL の未知キー（REQ-184）: 同梱定義に未知キーが無いこと、JSON Schema と YAML DTO のキー集合が一致すること。</summary>
public sealed class UnknownKeyIntegrationTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    public static TheoryData<string> BundledDefinitions()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(Path.Combine(RepoRoot, "formats"), "*.bdef.yaml", SearchOption.AllDirectories).Order())
            data.Add(Path.GetRelativePath(RepoRoot, path).Replace('\\', '/'));
        return data;
    }

    [Theory]
    [MemberData(nameof(BundledDefinitions))]
    public void BundledDefinition_HasNoUnknownKeys(string relativePath)
    {
        var path = Path.Combine(RepoRoot, relativePath);

        UnknownKeyScanner.Scan(File.ReadAllText(path), path).Should().BeEmpty();
    }

    /// <summary>Schema の定義（トップレベルは <c>"(root)"</c>）と、同じ場所を読む YAML DTO の対応。</summary>
    private static readonly Dictionary<string, IReadOnlySet<string>> DtoKeysBySchemaDefinition = new()
    {
        ["(root)"] = DocConsistency.YamlKeysOf<YamlFormatModel>(),
        ["import"] = DocConsistency.YamlKeysOf<YamlImportModel>(),
        ["enumEntry"] = DocConsistency.YamlKeysOf<YamlEnumEntry>(),
        ["flagsDefinition"] = DocConsistency.YamlKeysOf<YamlFlagsModel>(),
        ["flagField"] = DocConsistency.YamlKeysOf<YamlFlagFieldModel>(),
        ["structDefinition"] = DocConsistency.YamlKeysOf<YamlStructModel>(),
        ["field"] = DocConsistency.YamlKeysOf<YamlFieldModel>(),
        ["checksum"] = DocConsistency.YamlKeysOf<YamlChecksumModel>(),
        ["checksumRange"] = DocConsistency.YamlKeysOf<YamlChecksumRangeModel>(),
        ["bitfieldEntry"] = DocConsistency.YamlKeysOf<YamlBitfieldEntryModel>(),
    };

    private static JsonNode LoadSchema() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot, "schemas", "bdef.schema.json")))!;

    /// <summary>Schema の各定義の <c>properties</c> の名前集合と DTO の Alias 集合を比べ、不一致を返す。</summary>
    private static List<string> CompareSchemaWithDtos(JsonNode schema, IReadOnlyDictionary<string, IReadOnlySet<string>> dtoKeys)
    {
        var problems = new List<string>();
        var definitions = new Dictionary<string, JsonNode> { ["(root)"] = schema };
        foreach (var (name, def) in schema["$defs"]!.AsObject())
            definitions[name] = def!;

        foreach (var (name, def) in definitions)
        {
            if (!dtoKeys.TryGetValue(name, out var dto))
            {
                problems.Add($"Schema の定義 '{name}' に対応する YAML DTO がテストの対応表にありません");
                continue;
            }
            var schemaKeys = def["properties"]?.AsObject().Select(p => p.Key).ToHashSet() ?? [];
            foreach (var key in schemaKeys.Where(k => !dto.Contains(k)).Order())
                problems.Add($"Schema の '{name}' のキー '{key}' は YAML DTO にありません（ローダーは未知キー VAL123 として無視する）");
            foreach (var key in dto.Where(k => !schemaKeys.Contains(k)).Order())
                problems.Add($"YAML DTO のキー '{key}' は Schema の '{name}' にありません（エディタで誤りと表示される）");
        }
        foreach (var name in dtoKeys.Keys.Where(k => !definitions.ContainsKey(k)).Order())
            problems.Add($"対応表の '{name}' は Schema にありません");
        return problems;
    }

    [Fact]
    public void JsonSchema_KeysMatchYamlDtos()
    {
        CompareSchemaWithDtos(LoadSchema(), DtoKeysBySchemaDefinition).Should().BeEmpty();
    }

    [Fact]
    public void JsonSchema_DetectsKeyAddedOnlyToDto()
    {
        var keys = new Dictionary<string, IReadOnlySet<string>>(DtoKeysBySchemaDefinition)
        {
            ["field"] = new HashSet<string>(DtoKeysBySchemaDefinition["field"]) { "new_key" },
        };

        CompareSchemaWithDtos(LoadSchema(), keys).Should().ContainSingle(p => p.Contains("'new_key'"));
    }

    [Fact]
    public void JsonSchema_DetectsKeyAddedOnlyToSchema()
    {
        var schema = LoadSchema();
        schema["$defs"]!["checksum"]!["properties"]!.AsObject().Add("new_key", new JsonObject());

        CompareSchemaWithDtos(schema, DtoKeysBySchemaDefinition).Should().ContainSingle(p => p.Contains("'new_key'"));
    }
}
