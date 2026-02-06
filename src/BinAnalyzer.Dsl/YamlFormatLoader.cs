using BinAnalyzer.Core.Interfaces;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl.YamlModels;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace BinAnalyzer.Dsl;

public sealed class YamlFormatLoader : IFormatLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithNodeDeserializer(
            inner => new StructNodeDeserializer(inner),
            s => s.InsteadOf<YamlDotNet.Serialization.NodeDeserializers.ObjectNodeDeserializer>())
        .IgnoreUnmatchedProperties()
        .Build();

    public FormatDefinition Load(string path)
    {
        var resolvedPath = Path.GetFullPath(path);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var model = LoadAndResolveImports(resolvedPath, visited);
        return YamlToIrMapper.Map(model);
    }

    public FormatDefinition LoadFromString(string yaml)
    {
        var model = Deserializer.Deserialize<YamlFormatModel>(yaml);
        if (model.Imports is { Count: > 0 })
            throw new InvalidOperationException(
                "imports はファイルパスが必要です。Load(path) を使用してください。");
        return YamlToIrMapper.Map(model);
    }

    public FormatDefinition LoadFromString(string yaml, string basePath)
    {
        var model = Deserializer.Deserialize<YamlFormatModel>(yaml);
        if (model.Imports is { Count: > 0 })
        {
            var resolvedBase = Path.GetFullPath(basePath);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { resolvedBase };
            var baseDir = Path.GetDirectoryName(resolvedBase)!;
            foreach (var import in model.Imports)
            {
                var importPath = Path.GetFullPath(Path.Combine(baseDir, import.Path));
                if (!File.Exists(importPath))
                    throw new FileNotFoundException(
                        $"インポートファイルが見つかりません: {import.Path} (解決先: {importPath})");
                var imported = LoadAndResolveImports(importPath, visited);
                MergeDefinitions(model, imported, import.Path);
            }
        }
        return YamlToIrMapper.Map(model);
    }

    private YamlFormatModel LoadAndResolveImports(string absolutePath, HashSet<string> visited)
    {
        if (!visited.Add(absolutePath))
            throw new InvalidOperationException(
                $"循環インポートを検出しました: {absolutePath}");

        var yaml = File.ReadAllText(absolutePath);
        var model = Deserializer.Deserialize<YamlFormatModel>(yaml);

        if (model.Imports is null or { Count: 0 })
            return model;

        var baseDir = Path.GetDirectoryName(absolutePath)!;

        foreach (var import in model.Imports)
        {
            var importPath = Path.GetFullPath(Path.Combine(baseDir, import.Path));

            if (!File.Exists(importPath))
                throw new FileNotFoundException(
                    $"インポートファイルが見つかりません: {import.Path} (解決先: {importPath})");

            var imported = LoadAndResolveImports(importPath, visited);
            MergeDefinitions(model, imported, import.Path);
        }

        return model;
    }

    private static void MergeDefinitions(
        YamlFormatModel target, YamlFormatModel source, string sourcePath)
    {
        // Structs マージ
        if (source.Structs is { Count: > 0 })
        {
            foreach (var (name, structModel) in source.Structs)
            {
                if (target.Structs.ContainsKey(name))
                    throw new InvalidOperationException(
                        $"インポート '{sourcePath}' で定義名 '{name}' (struct) が重複しています");
                target.Structs[name] = structModel;
            }
        }

        // Enums マージ
        if (source.Enums is { Count: > 0 })
        {
            target.Enums ??= new();
            foreach (var (name, entries) in source.Enums)
            {
                if (target.Enums.ContainsKey(name))
                    throw new InvalidOperationException(
                        $"インポート '{sourcePath}' で定義名 '{name}' (enum) が重複しています");
                target.Enums[name] = entries;
            }
        }

        // Flags マージ
        if (source.Flags is { Count: > 0 })
        {
            target.Flags ??= new();
            foreach (var (name, flagsDef) in source.Flags)
            {
                if (target.Flags.ContainsKey(name))
                    throw new InvalidOperationException(
                        $"インポート '{sourcePath}' で定義名 '{name}' (flags) が重複しています");
                target.Flags[name] = flagsDef;
            }
        }
    }
}
