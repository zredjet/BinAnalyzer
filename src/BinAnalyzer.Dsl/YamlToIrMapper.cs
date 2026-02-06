using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl.YamlModels;

namespace BinAnalyzer.Dsl;

public static class YamlToIrMapper
{
    public static FormatDefinition Map(YamlFormatModel yaml)
    {
        var endianness = yaml.Endianness?.ToLowerInvariant() switch
        {
            "little" or "le" => Endianness.Little,
            "big" or "be" or null => Endianness.Big,
            _ => throw new InvalidOperationException($"Unknown endianness: {yaml.Endianness}"),
        };

        var enums = MapEnums(yaml.Enums);
        var flags = MapFlags(yaml.Flags);
        var structs = MapStructs(yaml.Structs);

        if (!structs.ContainsKey(yaml.Root))
            throw new InvalidOperationException($"Root struct '{yaml.Root}' not found in struct definitions");

        return new FormatDefinition
        {
            Name = yaml.Name,
            Endianness = endianness,
            Enums = enums,
            Flags = flags,
            Structs = structs,
            RootStruct = yaml.Root,
        };
    }

    private static IReadOnlyDictionary<string, EnumDefinition> MapEnums(
        Dictionary<string, List<YamlEnumEntry>>? yamlEnums)
    {
        if (yamlEnums is null)
            return new Dictionary<string, EnumDefinition>();

        var result = new Dictionary<string, EnumDefinition>();
        foreach (var (name, entries) in yamlEnums)
        {
            result[name] = new EnumDefinition
            {
                Name = name,
                Entries = entries.Select(e => new EnumEntry(e.Value, e.Label, e.Description)).ToList(),
            };
        }
        return result;
    }

    private static IReadOnlyDictionary<string, FlagsDefinition> MapFlags(
        Dictionary<string, YamlFlagsModel>? yamlFlags)
    {
        if (yamlFlags is null)
            return new Dictionary<string, FlagsDefinition>();

        var result = new Dictionary<string, FlagsDefinition>();
        foreach (var (name, model) in yamlFlags)
        {
            result[name] = new FlagsDefinition
            {
                Name = name,
                BitSize = model.BitSize,
                Fields = model.Fields.Select(f => new FlagFieldDefinition(
                    f.Name, f.Bit, f.BitSize, f.Set, f.Clear)).ToList(),
            };
        }
        return result;
    }

    private static IReadOnlyDictionary<string, StructDefinition> MapStructs(
        Dictionary<string, List<YamlFieldModel>> yamlStructs)
    {
        var result = new Dictionary<string, StructDefinition>();
        foreach (var (name, fields) in yamlStructs)
        {
            result[name] = new StructDefinition
            {
                Name = name,
                Fields = fields.Select(MapField).ToList(),
            };
        }
        return result;
    }

    private static FieldDefinition MapField(YamlFieldModel yaml)
    {
        var fieldType = ParseFieldType(yaml.Type);
        var (size, sizeExpr, sizeRemaining) = ParseSize(yaml.Size);
        var repeat = ParseRepeatMode(yaml);
        var (switchOn, switchCases, switchDefault) = ParseSwitch(yaml);

        return new FieldDefinition
        {
            Name = yaml.Name,
            Type = fieldType,
            Size = size,
            SizeExpression = sizeExpr,
            SizeRemaining = sizeRemaining,
            EnumRef = yaml.Enum,
            FlagsRef = yaml.Flags,
            StructRef = yaml.Struct,
            Repeat = repeat,
            SwitchOn = switchOn,
            SwitchCases = switchCases,
            SwitchDefault = switchDefault,
            BitfieldEntries = ParseBitfieldEntries(yaml.BitfieldFields),
            Checksum = ParseChecksum(yaml.Checksum),
            Expected = yaml.Expected?.Select(b => (byte)b).ToArray(),
            Condition = yaml.If is not null ? ExpressionParser.Parse(yaml.If) : null,
            Description = yaml.Description,
        };
    }

    private static FieldType ParseFieldType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "uint8" or "u8" => FieldType.UInt8,
            "uint16" or "u16" => FieldType.UInt16,
            "uint32" or "u32" => FieldType.UInt32,
            "uint64" or "u64" => FieldType.UInt64,
            "int8" or "i8" => FieldType.Int8,
            "int16" or "i16" => FieldType.Int16,
            "int32" or "i32" => FieldType.Int32,
            "int64" or "i64" => FieldType.Int64,
            "bytes" => FieldType.Bytes,
            "ascii" => FieldType.Ascii,
            "utf8" => FieldType.Utf8,
            "utf16le" or "utf16-le" => FieldType.Utf16Le,
            "utf16be" or "utf16-be" => FieldType.Utf16Be,
            "sjis" or "shift_jis" or "shift-jis" => FieldType.ShiftJis,
            "latin1" or "iso-8859-1" => FieldType.Latin1,
            "struct" => FieldType.Struct,
            "switch" => FieldType.Switch,
            "bitfield" => FieldType.Bitfield,
            _ => throw new InvalidOperationException($"Unknown field type: {type}"),
        };
    }

    private static (int? size, Expression? sizeExpr, bool sizeRemaining) ParseSize(string? sizeStr)
    {
        if (sizeStr is null)
            return (null, null, false);

        if (sizeStr.Equals("remaining", StringComparison.OrdinalIgnoreCase))
            return (null, null, true);

        if (int.TryParse(sizeStr, out var fixedSize))
            return (fixedSize, null, false);

        // Expression: e.g. "{length}" or "{length - 4}"
        var expr = ExpressionParser.Parse(sizeStr);
        return (null, expr, false);
    }

    private static RepeatMode ParseRepeatMode(YamlFieldModel yaml)
    {
        if (yaml.Repeat?.Equals("eof", StringComparison.OrdinalIgnoreCase) == true)
            return new RepeatMode.UntilEof();

        if (yaml.RepeatCount is not null)
        {
            var expr = ExpressionParser.Parse(yaml.RepeatCount);
            return new RepeatMode.Count(expr);
        }

        if (yaml.RepeatUntil is not null)
        {
            var expr = ExpressionParser.Parse(yaml.RepeatUntil);
            return new RepeatMode.UntilValue(expr);
        }

        return new RepeatMode.None();
    }

    private static (Expression? switchOn, IReadOnlyList<SwitchCase>? cases, string? defaultRef) ParseSwitch(
        YamlFieldModel yaml)
    {
        if (yaml.SwitchOn is null)
            return (null, null, null);

        var switchOn = ExpressionParser.Parse(yaml.SwitchOn);

        var cases = yaml.Cases?.Select(kvp =>
        {
            var condition = ExpressionParser.Parse(kvp.Key);
            return new SwitchCase(condition, kvp.Value);
        }).ToList() as IReadOnlyList<SwitchCase>;

        return (switchOn, cases, yaml.Default);
    }

    private static ChecksumSpec? ParseChecksum(YamlChecksumModel? yaml)
    {
        if (yaml is null)
            return null;

        return new ChecksumSpec
        {
            Algorithm = yaml.Algorithm,
            FieldNames = yaml.Fields,
        };
    }

    private static IReadOnlyList<BitfieldEntry>? ParseBitfieldEntries(
        List<YamlBitfieldEntryModel>? yamlEntries)
    {
        if (yamlEntries is null)
            return null;

        return yamlEntries.Select(e =>
        {
            var (high, low) = ParseBitsSpec(e.Bits);
            return new BitfieldEntry
            {
                Name = e.Name,
                BitHigh = high,
                BitLow = low,
                EnumRef = e.Enum,
                Description = e.Description,
            };
        }).ToList();
    }

    /// <summary>
    /// ビット範囲指定をパースする。
    /// "3" → (3, 3)（単一ビット）、"7:4" → (7, 4)（範囲）
    /// </summary>
    private static (int high, int low) ParseBitsSpec(string bits)
    {
        var parts = bits.Split(':');
        if (parts.Length == 1)
        {
            var bit = int.Parse(parts[0]);
            return (bit, bit);
        }

        var high = int.Parse(parts[0]);
        var low = int.Parse(parts[1]);
        return (high, low);
    }
}
