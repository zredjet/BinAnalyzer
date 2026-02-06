using YamlDotNet.Serialization;

namespace BinAnalyzer.Dsl.YamlModels;

public sealed class YamlFormatModel
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "";

    [YamlMember(Alias = "endianness")]
    public string? Endianness { get; set; }

    [YamlMember(Alias = "enums")]
    public Dictionary<string, List<YamlEnumEntry>>? Enums { get; set; }

    [YamlMember(Alias = "flags")]
    public Dictionary<string, YamlFlagsModel>? Flags { get; set; }

    [YamlMember(Alias = "structs")]
    public Dictionary<string, List<YamlFieldModel>> Structs { get; set; } = new();

    [YamlMember(Alias = "root")]
    public string Root { get; set; } = "";
}

public sealed class YamlEnumEntry
{
    [YamlMember(Alias = "value")]
    public long Value { get; set; }

    [YamlMember(Alias = "label")]
    public string Label { get; set; } = "";

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }
}

public sealed class YamlFlagsModel
{
    [YamlMember(Alias = "bit_size")]
    public int BitSize { get; set; }

    [YamlMember(Alias = "fields")]
    public List<YamlFlagFieldModel> Fields { get; set; } = new();
}

public sealed class YamlFlagFieldModel
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "";

    [YamlMember(Alias = "bit")]
    public int Bit { get; set; }

    [YamlMember(Alias = "bit_size")]
    public int BitSize { get; set; } = 1;

    [YamlMember(Alias = "set")]
    public string? Set { get; set; }

    [YamlMember(Alias = "clear")]
    public string? Clear { get; set; }
}

public sealed class YamlFieldModel
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "";

    [YamlMember(Alias = "type")]
    public string Type { get; set; } = "";

    [YamlMember(Alias = "size")]
    public string? Size { get; set; }

    [YamlMember(Alias = "enum")]
    public string? Enum { get; set; }

    [YamlMember(Alias = "flags")]
    public string? Flags { get; set; }

    [YamlMember(Alias = "struct")]
    public string? Struct { get; set; }

    [YamlMember(Alias = "repeat")]
    public string? Repeat { get; set; }

    [YamlMember(Alias = "repeat_count")]
    public string? RepeatCount { get; set; }

    [YamlMember(Alias = "repeat_until")]
    public string? RepeatUntil { get; set; }

    [YamlMember(Alias = "switch_on")]
    public string? SwitchOn { get; set; }

    [YamlMember(Alias = "cases")]
    public Dictionary<string, string>? Cases { get; set; }

    [YamlMember(Alias = "default")]
    public string? Default { get; set; }

    [YamlMember(Alias = "fields")]
    public List<YamlBitfieldEntryModel>? BitfieldFields { get; set; }

    [YamlMember(Alias = "if")]
    public string? If { get; set; }

    [YamlMember(Alias = "checksum")]
    public YamlChecksumModel? Checksum { get; set; }

    [YamlMember(Alias = "expected")]
    public List<int>? Expected { get; set; }

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }
}

public sealed class YamlChecksumModel
{
    [YamlMember(Alias = "algorithm")]
    public string Algorithm { get; set; } = "";

    [YamlMember(Alias = "fields")]
    public List<string> Fields { get; set; } = new();
}

public sealed class YamlBitfieldEntryModel
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "";

    [YamlMember(Alias = "bits")]
    public string Bits { get; set; } = "";

    [YamlMember(Alias = "enum")]
    public string? Enum { get; set; }

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }
}
