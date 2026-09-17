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

    /// <summary>ファイルから読み込む。imports はファイルシステム（<see cref="FileImportResolver"/>）で解決する。</summary>
    public FormatDefinition Load(string path)
    {
        var resolvedPath = Path.GetFullPath(path);
        var yaml = File.ReadAllText(resolvedPath);
        // FileImportResolver は同期完了するタスクしか返さないので、ここでブロックしてもデッドロックしない。
        return LoadAsync(yaml, resolvedPath, FileImportResolver.Instance).GetAwaiter().GetResult();
    }

    /// <summary>YAML テキストから読み込む。imports は解決できないため、含まれていれば例外。</summary>
    public FormatDefinition LoadFromString(string yaml)
    {
        var model = Deserializer.Deserialize<YamlFormatModel>(yaml);
        if (model.Imports is { Count: > 0 })
            throw new InvalidOperationException(
                "imports はファイルパスが必要です。Load(path) を使用してください。");
        return YamlToIrMapper.Map(model);
    }

    /// <summary>YAML テキストから読み込み、imports は <paramref name="basePath"/> のディレクトリ基準でファイルシステムから解決する。</summary>
    public FormatDefinition LoadFromString(string yaml, string basePath)
        => LoadAsync(yaml, Path.GetFullPath(basePath), FileImportResolver.Instance).GetAwaiter().GetResult();

    /// <summary>
    /// YAML テキストから読み込み、imports は <paramref name="resolver"/> で解決する。
    /// <paramref name="basePath"/> は YAML 自身の識別子（<see cref="FileImportResolver"/> なら絶対パス、HTTP なら相対 URL）で、
    /// 相対インポートの基準と循環検出の起点になる。
    /// </summary>
    public async Task<FormatDefinition> LoadAsync(string yaml, string basePath, IImportResolver resolver)
    {
        var model = Deserializer.Deserialize<YamlFormatModel>(yaml);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { basePath };
        await ResolveImportsAsync(model, basePath, resolver, visited).ConfigureAwait(false);
        return YamlToIrMapper.Map(model);
    }

    private static async Task ResolveImportsAsync(
        YamlFormatModel model, string basePath, IImportResolver resolver, HashSet<string> visited)
    {
        if (model.Imports is null or { Count: 0 })
            return;

        foreach (var import in model.Imports)
        {
            var resolved = resolver.Resolve(basePath, import.Path);

            if (!visited.Add(resolved))
                throw new InvalidOperationException(
                    $"循環インポートを検出しました: {resolved}");

            var yaml = await resolver.ReadAsync(resolved).ConfigureAwait(false)
                ?? throw new FileNotFoundException(
                    $"インポートファイルが見つかりません: {import.Path} (解決先: {resolved})");

            var imported = Deserializer.Deserialize<YamlFormatModel>(yaml);
            await ResolveImportsAsync(imported, resolved, resolver, visited).ConfigureAwait(false);
            MergeDefinitions(model, imported, import.Path);
        }
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
