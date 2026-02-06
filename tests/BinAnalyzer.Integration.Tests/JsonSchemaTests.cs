using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Json.Schema;
using Xunit;
using YamlDotNet.Serialization;

namespace BinAnalyzer.Integration.Tests;

public sealed class JsonSchemaTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static readonly string SchemaPath =
        Path.Combine(RepoRoot, "schemas", "bdef.schema.json");

    private static readonly string FormatsDir =
        Path.Combine(RepoRoot, "formats");

    private static readonly Lazy<JsonSchema> CachedSchema = new(() =>
    {
        var json = File.ReadAllText(SchemaPath);
        return JsonSchema.FromText(json);
    });

    private static JsonSchema LoadSchema() => CachedSchema.Value;

    private static JsonElement YamlFileToJsonElement(string yamlPath)
    {
        var yaml = File.ReadAllText(yamlPath);
        var deserializer = new DeserializerBuilder().Build();
        var yamlObj = deserializer.Deserialize(new StringReader(yaml))!;
        var jsonNode = ConvertToJsonNode(yamlObj);
        return JsonDocument.Parse(jsonNode!.ToJsonString()).RootElement;
    }

    private static JsonNode? ConvertToJsonNode(object? obj)
    {
        return obj switch
        {
            null => null,
            Dictionary<object, object> dict =>
                new JsonObject(dict.Select(kv =>
                    KeyValuePair.Create(kv.Key.ToString()!, ConvertToJsonNode(kv.Value)))),
            List<object> list =>
                new JsonArray(list.Select(ConvertToJsonNode).ToArray()),
            string s when long.TryParse(s, out var l) => JsonValue.Create(l),
            string s when s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(s.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var hex) => JsonValue.Create(hex),
            string s when s.StartsWith("0o", StringComparison.OrdinalIgnoreCase)
                && TryParseOctal(s.AsSpan(2), out var oct) => JsonValue.Create(oct),
            string s when double.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var d) => JsonValue.Create(d),
            string s when s == "true" => JsonValue.Create(true),
            string s when s == "false" => JsonValue.Create(false),
            string s => JsonValue.Create(s),
            _ => JsonValue.Create(obj.ToString()!),
        };
    }

    private static JsonElement ParseJson(string jsonStr)
    {
        return JsonDocument.Parse(jsonStr).RootElement;
    }

    [Fact]
    public void Schema_IsValidJson()
    {
        var json = File.ReadAllText(SchemaPath);
        var act = () => JsonDocument.Parse(json);
        act.Should().NotThrow();
    }

    [Fact]
    public void Schema_HasDraft202012()
    {
        var json = File.ReadAllText(SchemaPath);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("$schema").GetString()
            .Should().Be("https://json-schema.org/draft/2020-12/schema");
    }

    [Fact]
    public void Schema_HasAllFieldTypes()
    {
        var json = File.ReadAllText(SchemaPath);
        var doc = JsonDocument.Parse(json);

        var fieldDef = doc.RootElement
            .GetProperty("$defs")
            .GetProperty("field")
            .GetProperty("properties")
            .GetProperty("type")
            .GetProperty("enum");

        var types = fieldDef.EnumerateArray()
            .Select(e => e.GetString()!)
            .ToHashSet();

        var expected = new[]
        {
            "uint8", "u8", "uint16", "u16", "uint32", "u32", "uint64", "u64",
            "int8", "i8", "int16", "i16", "int32", "i32", "int64", "i64",
            "float32", "f32", "float64", "f64",
            "bytes", "ascii", "utf8",
            "utf16le", "utf16-le", "utf16be", "utf16-be",
            "sjis", "shift_jis", "shift-jis",
            "latin1", "iso-8859-1",
            "asciiz", "utf8z",
            "struct", "switch", "bitfield",
            "zlib", "deflate",
        };

        foreach (var t in expected)
            types.Should().Contain(t, $"type '{t}' should be in schema enum");
    }

    [Fact]
    public void Schema_RequiredProperties_TopLevel()
    {
        var json = File.ReadAllText(SchemaPath);
        var doc = JsonDocument.Parse(json);

        var required = doc.RootElement.GetProperty("required")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToList();

        required.Should().Contain("name");
        required.Should().Contain("root");
        required.Should().Contain("structs");
    }

    [Fact]
    public void Schema_RequiredProperties_Field()
    {
        var json = File.ReadAllText(SchemaPath);
        var doc = JsonDocument.Parse(json);

        var required = doc.RootElement
            .GetProperty("$defs")
            .GetProperty("field")
            .GetProperty("required")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToList();

        required.Should().Contain("name");
        required.Should().Contain("type");
    }

    [Fact]
    public void Schema_AllPropertiesHaveDescriptions()
    {
        var json = File.ReadAllText(SchemaPath);
        var doc = JsonDocument.Parse(json);

        var topProps = doc.RootElement.GetProperty("properties");
        foreach (var prop in topProps.EnumerateObject())
        {
            prop.Value.TryGetProperty("description", out _)
                .Should().BeTrue($"top-level property '{prop.Name}' should have description");
        }

        var fieldProps = doc.RootElement
            .GetProperty("$defs")
            .GetProperty("field")
            .GetProperty("properties");
        foreach (var prop in fieldProps.EnumerateObject())
        {
            prop.Value.TryGetProperty("description", out _)
                .Should().BeTrue($"field property '{prop.Name}' should have description");
        }
    }

    [Theory]
    [InlineData("png.bdef.yaml")]
    [InlineData("bmp.bdef.yaml")]
    [InlineData("wav.bdef.yaml")]
    [InlineData("zip.bdef.yaml")]
    [InlineData("elf.bdef.yaml")]
    public void Schema_ValidatesFormatFile(string fileName)
    {
        var schema = LoadSchema();
        var element = YamlFileToJsonElement(Path.Combine(FormatsDir, fileName));

        var result = schema.Evaluate(element, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
        });

        var errors = new List<string>();
        CollectErrors(result, errors);

        result.IsValid.Should().BeTrue(
            $"{fileName} should validate against schema. Errors:\n{string.Join("\n", errors)}");
    }

    private static bool TryParseOctal(ReadOnlySpan<char> s, out long result)
    {
        result = 0;
        foreach (var c in s)
        {
            if (c < '0' || c > '7') return false;
            result = result * 8 + (c - '0');
        }
        return s.Length > 0;
    }

    private static void CollectErrors(EvaluationResults results, List<string> errors)
    {
        if (!results.IsValid && results.Errors != null)
        {
            foreach (var e in results.Errors)
                errors.Add($"{results.InstanceLocation}: [{e.Key}] {e.Value}");
        }

        if (results.Details != null)
        {
            foreach (var detail in results.Details)
                CollectErrors(detail, errors);
        }
    }

    [Fact]
    public void Schema_RejectsInvalidFormat_MissingName()
    {
        var schema = LoadSchema();
        var element = ParseJson("""
        {
            "root": "test",
            "structs": {
                "test": [
                    { "name": "field1", "type": "uint8" }
                ]
            }
        }
        """);

        var result = schema.Evaluate(element);
        result.IsValid.Should().BeFalse("missing 'name' should fail validation");
    }

    [Fact]
    public void Schema_RejectsInvalidFormat_MissingRoot()
    {
        var schema = LoadSchema();
        var element = ParseJson("""
        {
            "name": "test",
            "structs": {
                "test": [
                    { "name": "field1", "type": "uint8" }
                ]
            }
        }
        """);

        var result = schema.Evaluate(element);
        result.IsValid.Should().BeFalse("missing 'root' should fail validation");
    }

    [Fact]
    public void Schema_RejectsInvalidFieldType()
    {
        var schema = LoadSchema();
        var element = ParseJson("""
        {
            "name": "test",
            "root": "test",
            "structs": {
                "test": [
                    { "name": "field1", "type": "invalid_type" }
                ]
            }
        }
        """);

        var result = schema.Evaluate(element);
        result.IsValid.Should().BeFalse("invalid type should fail validation");
    }

    [Fact]
    public void Schema_RejectsUnknownTopLevelProperty()
    {
        var schema = LoadSchema();
        var element = ParseJson("""
        {
            "name": "test",
            "root": "test",
            "structs": {
                "test": [
                    { "name": "field1", "type": "uint8" }
                ]
            },
            "unknown_prop": true
        }
        """);

        var result = schema.Evaluate(element);
        result.IsValid.Should().BeFalse("unknown top-level property should fail validation");
    }
}
