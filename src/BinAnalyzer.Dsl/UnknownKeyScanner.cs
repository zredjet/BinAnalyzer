using System.Collections.Concurrent;
using System.Reflection;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl.YamlModels;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace BinAnalyzer.Dsl;

/// <summary>
/// フォーマット定義の YAML から、DSL が受け付けないキー（デシリアライズで読み飛ばされるキー）を集める（REQ-184）。
/// YAML を表現モデルで読み、<see cref="YamlFormatModel"/> から DTO の型をたどって各マッピングのキーを
/// <c>[YamlMember(Alias)]</c> の集合と比べる。DTO にプロパティを足せば自動で既知のキーになる。
/// </summary>
public static class UnknownKeyScanner
{
    /// <summary>候補として示す最大の編集距離。</summary>
    private const int MaxSuggestionDistance = 2;

    private static readonly ConcurrentDictionary<Type, DtoInfo?> DtoCache = new();

    /// <summary>
    /// <paramref name="yaml"/> の未知キーを出現順に返す。<paramref name="sourceFile"/> は結果の <see cref="DslUnknownKey.SourceFile"/> になる。
    /// YAML として読めない場合は空（読めない YAML はデシリアライズ側が例外にする）。
    /// </summary>
    public static IReadOnlyList<DslUnknownKey> Scan(string yaml, string? sourceFile)
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return [];
        }

        var result = new List<DslUnknownKey>();
        foreach (var document in stream.Documents)
            Walk(document.RootNode, typeof(YamlFormatModel), new Location(TopLevel, null, null), sourceFile, result);
        return result;
    }

    private const string TopLevel = "トップレベル";

    /// <summary>キーがあった場所。<see cref="Describe"/> が説明文、struct / フィールド名は診断の StructName / FieldName になる。</summary>
    private sealed record Location(string Describe, string? StructName, string? FieldName)
    {
        /// <summary>この場所の中の <paramref name="part"/>。トップレベル直下は「トップレベル の」を付けない。英字で始まる部分の前には空白を入れる。</summary>
        public string Within(string part)
        {
            part = part.TrimStart();
            if (Describe == TopLevel)
                return part;
            return char.IsAscii(part[0]) ? $"{Describe} の {part}" : $"{Describe} の{part}";
        }
    }

    private static void Walk(YamlNode node, Type type, Location location, string? sourceFile, List<DslUnknownKey> result)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        // struct の旧形式（フィールドのリストを直接書く）。StructNodeDeserializer と同じ規則
        if (type == typeof(YamlStructModel) && node is YamlSequenceNode)
        {
            Walk(node, typeof(List<YamlFieldModel>), location, sourceFile, result);
            return;
        }

        if (GetDto(type) is { } dto)
        {
            if (node is not YamlMappingNode map)
                return;
            foreach (var (keyNode, value) in map.Children)
            {
                if (keyNode is not YamlScalarNode { Value: { } key })
                    continue;
                if (dto.Properties.TryGetValue(key, out var property))
                {
                    Walk(value, property.PropertyType, ChildLocation(location, key, property.PropertyType), sourceFile, result);
                }
                else
                {
                    result.Add(new DslUnknownKey(
                        key, location.Describe, location.StructName, location.FieldName,
                        Suggest(key, dto.Aliases), sourceFile, (int)keyNode.Start.Line));
                }
            }
            return;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) && node is YamlSequenceNode seq)
        {
            var itemType = type.GetGenericArguments()[0];
            for (var i = 0; i < seq.Children.Count; i++)
                Walk(seq.Children[i], itemType, ItemLocation(location, itemType, seq.Children[i], i), sourceFile, result);
            return;
        }

        // struct 名・enum 名などのキーは利用者の名前なので検査しない。値だけを型に沿ってたどる
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>) && node is YamlMappingNode dict)
        {
            var valueType = type.GetGenericArguments()[1];
            foreach (var (keyNode, value) in dict.Children)
            {
                var name = (keyNode as YamlScalarNode)?.Value ?? "?";
                Walk(value, valueType, EntryLocation(location, valueType, name), sourceFile, result);
            }
        }

        // それ以外（string / int / object など）は中を見ない
    }

    /// <summary>DTO の単一プロパティ（<c>checksum:</c> / <c>range:</c> 等）の中。コレクションは要素・値の側で場所を作る。</summary>
    private static Location ChildLocation(Location parent, string alias, Type propertyType) =>
        GetDto(Nullable.GetUnderlyingType(propertyType) ?? propertyType) is null
            ? parent
            : parent with { Describe = parent.Within(alias) };

    private static Location EntryLocation(Location parent, Type valueType, string name)
    {
        if (valueType == typeof(YamlStructModel))
            return new Location($"struct '{name}'", name, null);
        if (valueType == typeof(YamlFlagsModel))
            return new Location($"flags '{name}'", null, null);
        if (valueType == typeof(List<YamlEnumEntry>))
            return new Location($"enum '{name}'", null, null);
        return parent with { Describe = parent.Within($"'{name}'") };
    }

    private static Location ItemLocation(Location parent, Type itemType, YamlNode item, int index)
    {
        var name = item is YamlMappingNode map && map.Children.TryGetValue(new YamlScalarNode("name"), out var n)
            ? (n as YamlScalarNode)?.Value
            : null;
        var kind = itemType == typeof(YamlBitfieldEntryModel) ? "bitfield エントリ"
            : itemType == typeof(YamlEnumEntry) ? "エントリ"
            : itemType == typeof(YamlImportModel) ? "インポート"
            : itemType == typeof(YamlChecksumRangeModel) ? "範囲"
            : "フィールド";
        var describe = name is null
            ? parent.Within($"{index + 1} 番目の{kind}")
            : parent.Within($"{kind} '{name}'");

        // struct 直下のフィールドだけが診断の FieldName になる（bitfield エントリ等は親フィールドのまま）
        var fieldName = itemType == typeof(YamlFieldModel) && parent.StructName is not null && parent.FieldName is null
            ? name
            : parent.FieldName;
        return parent with { Describe = describe, FieldName = fieldName };
    }

    /// <summary>
    /// 同じ場所で受け付けるキーのうち、<paramref name="key"/> との編集距離（大文字小文字を無視）が最小で
    /// <see cref="MaxSuggestionDistance"/> 以内のもの。同点は定義順で先のもの。
    /// </summary>
    internal static string? Suggest(string key, IReadOnlyList<string> candidates)
    {
        string? best = null;
        var bestDistance = MaxSuggestionDistance + 1;
        foreach (var candidate in candidates)
        {
            var d = Levenshtein(key.ToLowerInvariant(), candidate.ToLowerInvariant());
            if (d < bestDistance)
            {
                best = candidate;
                bestDistance = d;
            }
        }
        return best;
    }

    private static int Levenshtein(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[b.Length];
    }

    /// <summary>DTO（<c>[YamlMember(Alias)]</c> を持つクラス）の Alias → プロパティ。DTO でなければ null。</summary>
    private static DtoInfo? GetDto(Type type) => DtoCache.GetOrAdd(type, static t =>
    {
        if (!t.IsClass || t == typeof(string))
            return null;
        var properties = new Dictionary<string, PropertyInfo>();
        var aliases = new List<string>();
        foreach (var property in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<YamlMemberAttribute>()?.Alias is { } alias)
            {
                properties[alias] = property;
                aliases.Add(alias);
            }
        }
        return properties.Count == 0 ? null : new DtoInfo(properties, aliases);
    });

    private sealed record DtoInfo(IReadOnlyDictionary<string, PropertyInfo> Properties, IReadOnlyList<string> Aliases);
}
