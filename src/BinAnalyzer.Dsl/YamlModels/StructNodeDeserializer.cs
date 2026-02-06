using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace BinAnalyzer.Dsl.YamlModels;

/// <summary>
/// YamlStructModel のカスタムデシリアライザ。
/// 旧形式（フィールドリスト直接）と新形式（endianness/align/fields を持つオブジェクト）の両方をサポートする。
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

        // YamlStructModel の MappingStart、または他の全型 → フォールバック
        return _fallback.Deserialize(reader, expectedType, nestedObjectDeserializer, out value, rootDeserializer);
    }
}
