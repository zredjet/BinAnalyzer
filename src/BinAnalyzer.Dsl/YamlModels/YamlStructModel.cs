using YamlDotNet.Serialization;

namespace BinAnalyzer.Dsl.YamlModels;

public sealed class YamlStructModel
{
    [YamlMember(Alias = "endianness")]
    public string? Endianness { get; set; }

    [YamlMember(Alias = "align")]
    public int? Align { get; set; }

    [YamlMember(Alias = "string_table")]
    public bool? StringTable { get; set; }

    [YamlMember(Alias = "fields")]
    public List<YamlFieldModel> Fields { get; set; } = new();
}
