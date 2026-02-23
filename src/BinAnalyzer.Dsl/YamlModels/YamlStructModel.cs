using YamlDotNet.Serialization;

namespace BinAnalyzer.Dsl.YamlModels;

public sealed class YamlStructModel
{
    [YamlMember(Alias = "endianness")]
    public string? Endianness { get; set; }

    [YamlMember(Alias = "align")]
    public int? Align { get; set; }

    [YamlMember(Alias = "string_table")]
    public object? StringTable { get; set; }

    [YamlMember(Alias = "mode")]
    public string? Mode { get; set; }

    [YamlMember(Alias = "bit_order")]
    public string? BitOrder { get; set; }

    [YamlMember(Alias = "resync_marker")]
    public List<int>? ResyncMarker { get; set; }

    [YamlMember(Alias = "fields")]
    public List<YamlFieldModel> Fields { get; set; } = new();
}
