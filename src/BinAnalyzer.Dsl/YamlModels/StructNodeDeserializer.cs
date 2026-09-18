using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace BinAnalyzer.Dsl.YamlModels;

/// <summary>
/// YamlStructModel のカスタムデシリアライザ。
/// 旧形式（フィールドリスト直接）と新形式（endianness/align/fields を持つオブジェクト）の両方をサポートする。
/// あわせて、struct 名（キー）とフィールドの開始行を <see cref="YamlStructModel.SourceLine"/> /
/// <see cref="YamlFieldModel.SourceLine"/> に記録する（REQ-172）。
/// YamlStructModel 以外の型はフォールバックデシリアライザに委譲する。
/// </summary>
public sealed class StructNodeDeserializer : INodeDeserializer
{
    private readonly INodeDeserializer _fallback;

    public StructNodeDeserializer(INodeDeserializer fallback)
    {
        _fallback = fallback;
    }

    public bool Deserialize(IParser reader, Type expectedType, Func<IParser, Type, object?> nestedObjectDeserializer, out object? value,
        ObjectDeserializer rootDeserializer)
    {
        if (expectedType == typeof(YamlStructModel))
        {
            // SequenceStart → 旧形式（フィールドリスト直接）
            if (reader.Accept<SequenceStart>(out _))
            {
                var fields = (List<YamlFieldModel>)nestedObjectDeserializer(reader, typeof(List<YamlFieldModel>))!;
                value = new YamlStructModel { Fields = fields };
                return true;
            }
        }

        var startLine = reader.Current?.Start.Line;

        // YamlStructModel の MappingStart、または他の全型 → フォールバック
        var handled = _fallback.Deserialize(reader, expectedType, nestedObjectDeserializer, out value, rootDeserializer);
        if (handled && value is YamlFieldModel field && startLine is { } line)
            field.SourceLine = (int)line;
        return handled;
    }

}

/// <summary>
/// <c>structs:</c> のマッピング（<c>Dictionary&lt;string, YamlStructModel&gt;</c>）を自前に走査し、キー（struct 名）の行を
/// <see cref="YamlStructModel.SourceLine"/> に記録する（REQ-172）。標準の DictionaryNodeDeserializer より前に置く必要がある。
/// </summary>
public sealed class StructsDictionaryDeserializer : INodeDeserializer
{
    public bool Deserialize(IParser reader, Type expectedType, Func<IParser, Type, object?> nestedObjectDeserializer, out object? value,
        ObjectDeserializer rootDeserializer)
    {
        if (expectedType != typeof(Dictionary<string, YamlStructModel>) || !reader.Accept<MappingStart>(out _))
        {
            value = null;
            return false;
        }

        var result = new Dictionary<string, YamlStructModel>();
        reader.Consume<MappingStart>();
        while (!reader.TryConsume<MappingEnd>(out _))
        {
            var key = reader.Consume<Scalar>();
            var model = (YamlStructModel?)nestedObjectDeserializer(reader, typeof(YamlStructModel))
                ?? throw new YamlException(key.Start, key.End, $"struct '{key.Value}' の定義が空です");
            model.SourceLine = (int)key.Start.Line;
            result[key.Value] = model;
        }
        value = result;
        return true;
    }
}
