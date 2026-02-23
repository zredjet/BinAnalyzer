using System.Collections;
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
        Dictionary<string, YamlStructModel> yamlStructs)
    {
        var result = new Dictionary<string, StructDefinition>();
        foreach (var (rawKey, structModel) in yamlStructs)
        {
            var (name, parameters) = ParseStructKey(rawKey);

            Expression? endiannessExpr = null;
            Endianness? endianness = null;

            if (structModel.Endianness is not null && structModel.Endianness.StartsWith('{'))
            {
                endiannessExpr = ExpressionParser.Parse(structModel.Endianness);
            }
            else
            {
                endianness = ParseEndianness(structModel.Endianness);
            }

            // mode バリデーション
            if (structModel.Mode is not null and not "bitstream")
                throw new InvalidOperationException(
                    $"Unknown struct mode '{structModel.Mode}' in struct '{name}'. Supported modes: bitstream");

            var isBitstream = structModel.Mode == "bitstream";

            // bit_order パースとバリデーション
            BitOrder? parsedBitOrder = structModel.BitOrder?.ToLowerInvariant() switch
            {
                null => null,
                "msb" => BitOrder.Msb,
                "lsb" => BitOrder.Lsb,
                _ => throw new InvalidOperationException(
                    $"Unknown bit_order '{structModel.BitOrder}' in struct '{name}'. Supported values: msb, lsb"),
            };

            if (parsedBitOrder is not null && !isBitstream)
                throw new InvalidOperationException(
                    $"bit_order is specified on struct '{name}' but mode is not 'bitstream'. " +
                    $"bit_order is only valid with mode: bitstream");

            var fields = structModel.Fields.Select(MapField).ToList();

            // bitstream フィールド型バリデーション
            if (isBitstream)
            {
                foreach (var field in fields)
                {
                    if (!IsAllowedInBitstreamStruct(field.Type))
                        throw new InvalidOperationException(
                            $"Bitstream struct '{name}' contains unsupported field '{field.Name}' of type '{field.Type}'. " +
                            $"Allowed types in bitstream structs: integer types, virtual, switch, struct.");
                }
            }

            result[name] = new StructDefinition
            {
                Name = name,
                Fields = fields,
                Parameters = parameters,
                Endianness = endianness,
                EndiannessExpression = endiannessExpr,
                Align = structModel.Align,
                StringTableEncoding = ParseStringTable(structModel.StringTable),
                IsBitstream = isBitstream,
                BitOrder = parsedBitOrder,
                ResyncMarker = structModel.ResyncMarker?.Select(b => (byte)b).ToArray(),
            };
        }
        return result;
    }

    private static Endianness? ParseEndianness(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "little" or "le" => Endianness.Little,
            "big" or "be" => Endianness.Big,
            null => null,
            _ => throw new InvalidOperationException($"Unknown endianness: {value}"),
        };
    }

    private static FieldDefinition MapField(YamlFieldModel yaml)
    {
        var fieldType = ParseFieldType(yaml.Type);
        var (size, sizeExpr, sizeRemaining) = ParseSize(yaml.Size);
        var repeat = ParseRepeatMode(yaml);
        var (switchOn, switchCases, switchDefault) = ParseSwitch(yaml);
        var (elementSize, elementSizeExpr) = ParseElementSize(yaml.ElementSize);
        var (structRef, structArgs) = ParseStructRef(yaml.Struct);

        return new FieldDefinition
        {
            Name = yaml.Name,
            Type = fieldType,
            Size = size,
            SizeExpression = sizeExpr,
            SizeRemaining = sizeRemaining,
            EnumRef = yaml.Enum,
            FlagsRef = yaml.Flags,
            StructRef = structRef,
            StructArgs = structArgs,
            Repeat = repeat,
            SwitchOn = switchOn,
            SwitchCases = switchCases,
            SwitchDefault = switchDefault,
            BitfieldEntries = ParseBitfieldEntries(yaml.BitfieldFields),
            Checksum = ParseChecksum(yaml.Checksum),
            Expected = yaml.Expected?.Select(b => (byte)b).ToArray(),
            Condition = yaml.If is not null ? ExpressionParser.Parse(yaml.If) : null,
            Description = yaml.Description,
            Align = yaml.Align,
            IsPadding = yaml.Padding ?? false,
            ElementSize = elementSize,
            ElementSizeExpression = elementSizeExpr,
            ValueExpression = yaml.Value is not null ? ExpressionParser.Parse(yaml.Value) : null,
            SeekExpression = yaml.Seek is not null ? ExpressionParser.Parse(yaml.Seek) : null,
            SeekRestore = yaml.SeekRestore ?? false,
            SeekBaseExpression = yaml.SeekBase is not null ? ExpressionParser.Parse(yaml.SeekBase) : null,
            Endianness = ParseEndianness(yaml.Endianness),
            ValidationExpression = yaml.Validate is not null ? ExpressionParser.Parse(yaml.Validate) : null,
            StringTableRef = yaml.StringTable,
            DiffKey = MapDiffKey(yaml.DiffKey),
            State = yaml.State,
            StateIf = yaml.StateIf is not null ? ExpressionParser.Parse(yaml.StateIf) : null,
            StateDefault = yaml.StateDefault,
            RepeatMax = yaml.RepeatMax is not null ? ExpressionParser.Parse(yaml.RepeatMax) : null,
            RepeatErrorLimit = yaml.RepeatErrorLimit is not null ? ExpressionParser.Parse(yaml.RepeatErrorLimit) : null,
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
            "asciiz" => FieldType.AsciiZ,
            "utf8z" => FieldType.Utf8Z,
            "float32" or "f32" => FieldType.Float32,
            "float64" or "f64" => FieldType.Float64,
            "struct" => FieldType.Struct,
            "switch" => FieldType.Switch,
            "bitfield" => FieldType.Bitfield,
            "zlib" => FieldType.Zlib,
            "deflate" => FieldType.Deflate,
            "gzip" => FieldType.Gzip,
            "bzip2" => FieldType.Bzip2,
            "lzma" => FieldType.Lzma,
            "zstd" or "zstandard" => FieldType.Zstd,
            "lz4" => FieldType.Lz4,
            "virtual" => FieldType.Virtual,
            "uleb128" or "leb128u" => FieldType.ULeb128,
            "sleb128" or "leb128s" => FieldType.SLeb128,
            "vlq" => FieldType.Vlq,
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

    private static (int? size, Expression? sizeExpr) ParseElementSize(string? sizeStr)
    {
        if (sizeStr is null)
            return (null, null);

        if (int.TryParse(sizeStr, out var fixedSize))
            return (fixedSize, null);

        var expr = ExpressionParser.Parse(sizeStr);
        return (null, expr);
    }

    private static RepeatMode ParseRepeatMode(YamlFieldModel yaml)
    {
        if (yaml.Repeat?.Equals("eof", StringComparison.OrdinalIgnoreCase) == true)
            return new RepeatMode.UntilEof();

        if (yaml.Repeat?.Equals("while", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (yaml.RepeatWhile is null)
                throw new InvalidOperationException(
                    $"Field with 'repeat: while' requires 'repeat_while' expression");
            var expr = ExpressionParser.Parse(yaml.RepeatWhile);
            return new RepeatMode.While(expr);
        }

        if (yaml.Repeat?.Equals("length_prefixed", StringComparison.OrdinalIgnoreCase) == true)
        {
            var prefixSize = yaml.LengthPrefixSize ?? 1;
            if (prefixSize < 1 || prefixSize > 4)
                throw new InvalidOperationException(
                    $"Field with 'repeat: length_prefixed' requires 'length_prefix_size' between 1 and 4, got {prefixSize}");
            return new RepeatMode.LengthPrefixed(prefixSize);
        }

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

        if (yaml.RepeatWhile is not null)
        {
            var expr = ExpressionParser.Parse(yaml.RepeatWhile);
            return new RepeatMode.While(expr);
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

        ChecksumRange? range = yaml.Range is not null
            ? new ChecksumRange
            {
                OffsetExpression = ExpressionParser.Parse(yaml.Range.Offset),
                SizeExpression = ExpressionParser.Parse(yaml.Range.Size),
            }
            : null;

        IReadOnlyList<ChecksumRange>? ranges = yaml.Ranges?.Select(r => new ChecksumRange
        {
            OffsetExpression = ExpressionParser.Parse(r.Offset),
            SizeExpression = ExpressionParser.Parse(r.Size),
        }).ToList();

        return new ChecksumSpec
        {
            Algorithm = yaml.Algorithm,
            FieldNames = yaml.Fields,
            Range = range,
            Ranges = ranges,
            ExcludeSelf = yaml.ExcludeSelf ?? false,
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

    private static StringTableEncoding? ParseStringTable(object? value)
    {
        return value switch
        {
            null => null,
            bool b => b ? StringTableEncoding.Ascii : null,
            string s when s.Equals("true", StringComparison.OrdinalIgnoreCase) => StringTableEncoding.Ascii,
            string s when s.Equals("false", StringComparison.OrdinalIgnoreCase) => null,
            IDictionary dict => ParseStringTableDict(dict),
            _ => throw new InvalidOperationException(
                $"Invalid string_table value: '{value}'. Expected true, false, or {{ encoding: ... }}"),
        };
    }

    private static StringTableEncoding ParseStringTableDict(IDictionary dict)
    {
        var encodingValue = dict.Contains("encoding") ? dict["encoding"]?.ToString() : null;
        return encodingValue?.ToLowerInvariant() switch
        {
            null or "ascii" => StringTableEncoding.Ascii,
            "utf8" or "utf-8" => StringTableEncoding.Utf8,
            "utf16le" or "utf-16le" or "utf16-le" => StringTableEncoding.Utf16Le,
            "utf16be" or "utf-16be" or "utf16-be" => StringTableEncoding.Utf16Be,
            _ => throw new InvalidOperationException(
                $"Unknown string_table encoding: '{encodingValue}'. Supported: ascii, utf8, utf16le, utf16be"),
        };
    }

    private static IReadOnlyList<string>? MapDiffKey(object? value)
    {
        return value switch
        {
            string s => [s],
            IList list => list.Cast<object>().Select(x => x.ToString()!).ToArray(),
            _ => null,
        };
    }

    private static bool IsIntegerFieldType(FieldType type) =>
        type is FieldType.UInt8 or FieldType.UInt16 or FieldType.UInt32 or FieldType.UInt64
            or FieldType.Int8 or FieldType.Int16 or FieldType.Int32 or FieldType.Int64;

    private static bool IsAllowedInBitstreamStruct(FieldType type) =>
        IsIntegerFieldType(type)
        || type is FieldType.Virtual or FieldType.Switch or FieldType.Struct;

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

    /// <summary>
    /// 構造体定義キーをパースする。
    /// "tlv(tag_size=1, len_size=1)" → ("tlv", [TemplateParameter("tag_size", 1), TemplateParameter("len_size", 1)])
    /// "record(size)" → ("record", [TemplateParameter("size", null)])
    /// "simple" → ("simple", [])
    /// </summary>
    internal static (string name, IReadOnlyList<TemplateParameter> parameters) ParseStructKey(string key)
    {
        var parenIndex = key.IndexOf('(');
        if (parenIndex < 0)
            return (key, Array.Empty<TemplateParameter>());

        var name = key[..parenIndex].Trim();
        var closeParen = key.LastIndexOf(')');
        if (closeParen < parenIndex)
            throw new InvalidOperationException($"Invalid struct key syntax: '{key}' — missing closing ')'");

        var argsStr = key[(parenIndex + 1)..closeParen];
        var parts = SplitArguments(argsStr);
        var parameters = new List<TemplateParameter>();

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex >= 0)
            {
                var paramName = trimmed[..eqIndex].Trim();
                var defaultStr = trimmed[(eqIndex + 1)..].Trim();
                var defaultValue = long.Parse(defaultStr);
                parameters.Add(new TemplateParameter(paramName, defaultValue));
            }
            else
            {
                parameters.Add(new TemplateParameter(trimmed, null));
            }
        }

        return (name, parameters);
    }

    /// <summary>
    /// フィールドのstruct参照文字列をパースする。
    /// "tlv(tag_size=1, len_size=2)" → ("tlv", [StructArgument("tag_size", 1, null), ...])
    /// "tlv(1, 2)" → ("tlv", [StructArgument(null, 1, null), ...])
    /// "tlv({field_ref})" → ("tlv", [StructArgument(null, null, Expression)])
    /// "simple" → ("simple", null)
    /// </summary>
    internal static (string? structRef, IReadOnlyList<StructArgument>? structArgs) ParseStructRef(string? rawRef)
    {
        if (rawRef is null)
            return (null, null);

        var parenIndex = rawRef.IndexOf('(');
        if (parenIndex < 0)
            return (rawRef, null);

        var name = rawRef[..parenIndex].Trim();
        var closeParen = rawRef.LastIndexOf(')');
        if (closeParen < parenIndex)
            throw new InvalidOperationException($"Invalid struct reference syntax: '{rawRef}' — missing closing ')'");

        var argsStr = rawRef[(parenIndex + 1)..closeParen];
        var parts = SplitArguments(argsStr);
        var args = new List<StructArgument>();

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // 名前付き引数のチェック: "param_name=value" or "param_name={expr}"
            // ただし "{expr}" の中の = は名前付き引数ではない
            string? paramName = null;
            string valueStr = trimmed;
            var eqIndex = FindNamedArgEquals(trimmed);
            if (eqIndex >= 0)
            {
                paramName = trimmed[..eqIndex].Trim();
                valueStr = trimmed[(eqIndex + 1)..].Trim();
            }

            // 式かリテラルかを判定
            if (valueStr.Contains('{'))
            {
                var expr = ExpressionParser.Parse(valueStr);
                args.Add(new StructArgument(paramName, null, expr));
            }
            else if (long.TryParse(valueStr, out var longValue))
            {
                args.Add(new StructArgument(paramName, longValue, null));
            }
            else
            {
                throw new InvalidOperationException(
                    $"Invalid struct argument value: '{valueStr}' in '{rawRef}'");
            }
        }

        return (name, args.Count > 0 ? args : null);
    }

    /// <summary>
    /// 名前付き引数の = 位置を検索する。{...} 内の = は無視する。
    /// </summary>
    private static int FindNamedArgEquals(string s)
    {
        var braceDepth = 0;
        for (var i = 0; i < s.Length; i++)
        {
            switch (s[i])
            {
                case '{': braceDepth++; break;
                case '}': braceDepth--; break;
                case '=' when braceDepth == 0:
                    // '==' は比較演算子なのでスキップ
                    if (i + 1 < s.Length && s[i + 1] == '=')
                        { i++; continue; }
                    return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// ブレース深度を考慮してカンマで分割する。
    /// "tag_size=1, len_size={x + 1}" → ["tag_size=1", "len_size={x + 1}"]
    /// </summary>
    internal static List<string> SplitArguments(string argsString)
    {
        var result = new List<string>();
        var braceDepth = 0;
        var start = 0;

        for (var i = 0; i < argsString.Length; i++)
        {
            switch (argsString[i])
            {
                case '{': braceDepth++; break;
                case '}': braceDepth--; break;
                case ',' when braceDepth == 0:
                    result.Add(argsString[start..i]);
                    start = i + 1;
                    break;
            }
        }

        if (start < argsString.Length)
            result.Add(argsString[start..]);

        return result;
    }
}
