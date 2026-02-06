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
        .IgnoreUnmatchedProperties()
        .Build();

    public FormatDefinition Load(string path)
    {
        var yaml = File.ReadAllText(path);
        return LoadFromString(yaml);
    }

    public FormatDefinition LoadFromString(string yaml)
    {
        var model = Deserializer.Deserialize<YamlFormatModel>(yaml);
        return YamlToIrMapper.Map(model);
    }
}
