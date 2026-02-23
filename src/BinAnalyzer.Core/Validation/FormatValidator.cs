using BinAnalyzer.Core.Models;

namespace BinAnalyzer.Core.Validation;

/// <summary>
/// FormatDefinition の静的バリデーションを行う。
/// </summary>
public static class FormatValidator
{
    public static ValidationResult Validate(FormatDefinition format)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        foreach (var (structName, structDef) in format.Structs)
        {
            foreach (var field in structDef.Fields)
            {
                ValidateStructRef(field, structName, format, diagnostics);
                ValidateSwitchSpec(field, structName, format, diagnostics);
                ValidateSizeSpec(field, structName, diagnostics);
                ValidateBitfieldSize(field, structName, diagnostics);
                ValidateEnumRef(field, structName, format, diagnostics);
                ValidateFlagsRef(field, structName, format, diagnostics);
                ValidateTypeRefCombination(field, structName, diagnostics);
                ValidateAlign(field, structName, diagnostics);
                ValidateElementSize(field, structName, diagnostics);
                ValidateVirtualField(field, structName, diagnostics);
                ValidateSeek(field, structName, diagnostics);
                ValidateStringTable(field, structName, diagnostics);
                ValidateLengthPrefixed(field, structName, diagnostics);
                ValidateChecksum(field, structName, diagnostics);
                ValidateState(field, structName, diagnostics);
                ValidateRepeatGuard(field, structName, diagnostics);
                ValidateBitstreamCrossRef(field, structName, structDef, format, diagnostics);
            }

            ValidateBitOrderOnNonBitstream(structDef, diagnostics);
            ValidateStructAlign(structDef, diagnostics);
        }

        ValidateUnusedEnums(format, diagnostics);
        ValidateUnusedFlags(format, diagnostics);
        ValidateUnreachableStructs(format, diagnostics);

        return new ValidationResult(diagnostics);
    }

    // --- エラー VAL001-VAL007 ---

    /// <summary>VAL001: struct型フィールドの StructRef が未指定</summary>
    /// <summary>VAL002: StructRef が存在しないstruct名を参照</summary>
    /// <summary>VAL116: テンプレートstructの必須パラメータ未指定</summary>
    private static void ValidateStructRef(
        FieldDefinition field, string structName,
        FormatDefinition format, List<ValidationDiagnostic> diagnostics)
    {
        if (field.Type == FieldType.Struct)
        {
            if (field.StructRef is null)
            {
                diagnostics.Add(Error("VAL001",
                    $"struct型フィールド '{field.Name}' に struct 参照が指定されていません",
                    structName, field.Name));
            }
            else if (!format.Structs.ContainsKey(field.StructRef))
            {
                diagnostics.Add(Error("VAL002",
                    $"フィールド '{field.Name}' が参照するstruct '{field.StructRef}' は定義されていません",
                    structName, field.Name));
            }
            else
            {
                ValidateTemplateArgs(field, structName, format, diagnostics);
            }
        }
        else if (field.Type != FieldType.Switch && field.StructRef is not null)
        {
            if (!format.Structs.ContainsKey(field.StructRef))
            {
                diagnostics.Add(Error("VAL002",
                    $"フィールド '{field.Name}' が参照するstruct '{field.StructRef}' は定義されていません",
                    structName, field.Name));
            }
        }
    }

    /// <summary>VAL116: テンプレートstructの必須パラメータが引数なしで参照されている</summary>
    private static void ValidateTemplateArgs(
        FieldDefinition field, string structName,
        FormatDefinition format, List<ValidationDiagnostic> diagnostics)
    {
        if (field.StructRef is null || !format.Structs.TryGetValue(field.StructRef, out var targetStruct))
            return;

        var requiredParams = targetStruct.Parameters
            .Where(p => !p.DefaultValue.HasValue)
            .ToList();

        if (requiredParams.Count == 0)
            return;

        if (field.StructArgs is null || field.StructArgs.Count == 0)
        {
            var paramNames = string.Join(", ", requiredParams.Select(p => p.Name));
            diagnostics.Add(Warning("VAL116",
                $"フィールド '{field.Name}' がテンプレートstruct '{field.StructRef}' を引数なしで参照していますが、" +
                $"必須パラメータ ({paramNames}) が未指定です",
                structName, field.Name));
        }
    }

    /// <summary>VAL003-VAL006: switch型フィールドの検証</summary>
    private static void ValidateSwitchSpec(
        FieldDefinition field, string structName,
        FormatDefinition format, List<ValidationDiagnostic> diagnostics)
    {
        if (field.Type != FieldType.Switch)
            return;

        // VAL005: switch_on が未指定
        if (field.SwitchOn is null)
        {
            diagnostics.Add(Error("VAL005",
                $"switch型フィールド '{field.Name}' に switch_on が指定されていません",
                structName, field.Name));
        }

        // VAL006: cases も default もない
        if ((field.SwitchCases is null || field.SwitchCases.Count == 0) && field.SwitchDefault is null)
        {
            diagnostics.Add(Error("VAL006",
                $"switch型フィールド '{field.Name}' に cases も default も指定されていません",
                structName, field.Name));
        }

        // VAL003: switch case の参照先structが未定義
        if (field.SwitchCases is not null)
        {
            foreach (var switchCase in field.SwitchCases)
            {
                if (!format.Structs.ContainsKey(switchCase.StructRef))
                {
                    diagnostics.Add(Error("VAL003",
                        $"フィールド '{field.Name}' の switch case が参照するstruct '{switchCase.StructRef}' は定義されていません",
                        structName, field.Name));
                }
            }
        }

        // VAL004: switch default の参照先structが未定義
        if (field.SwitchDefault is not null && !format.Structs.ContainsKey(field.SwitchDefault))
        {
            diagnostics.Add(Error("VAL004",
                $"フィールド '{field.Name}' の switch default が参照するstruct '{field.SwitchDefault}' は定義されていません",
                structName, field.Name));
        }

        // VAL105: switch に default がない（警告）
        if (field.SwitchDefault is null)
        {
            diagnostics.Add(Warning("VAL105",
                $"switch型フィールド '{field.Name}' に default が指定されていません（マッチしない値がある場合ランタイムエラーになります）",
                structName, field.Name));
        }
    }

    /// <summary>VAL007: サイズ指定が必要な型でサイズ未指定（Virtual型、LengthPrefixed繰り返しは除外）</summary>
    private static void ValidateSizeSpec(
        FieldDefinition field, string structName,
        List<ValidationDiagnostic> diagnostics)
    {
        if (field.Type == FieldType.Virtual)
            return;

        if (field.Repeat is RepeatMode.LengthPrefixed)
            return;

        if (field.Type is FieldType.Bytes or FieldType.Ascii or FieldType.Utf8
                or FieldType.Utf16Le or FieldType.Utf16Be or FieldType.ShiftJis or FieldType.Latin1
                or FieldType.Bitfield or FieldType.Zlib or FieldType.Deflate
                or FieldType.Gzip or FieldType.Bzip2 or FieldType.Lzma or FieldType.Zstd or FieldType.Lz4)
        {
            if (!field.Size.HasValue && field.SizeExpression is null && !field.SizeRemaining)
            {
                diagnostics.Add(Error("VAL007",
                    $"フィールド '{field.Name}' ({field.Type}) にサイズ指定がありません（size, size式, remaining のいずれかが必要です）",
                    structName, field.Name));
            }
        }
    }

    /// <summary>VAL013: bitfieldのサイズが1〜8バイトの範囲外</summary>
    private static void ValidateBitfieldSize(
        FieldDefinition field, string structName,
        List<ValidationDiagnostic> diagnostics)
    {
        if (field.Type != FieldType.Bitfield)
            return;

        if (field.Size is { } size && (size < 1 || size > 8))
        {
            diagnostics.Add(Error("VAL013",
                $"bitfieldフィールド '{field.Name}' のサイズは1〜8バイトが必要です: {size}",
                structName, field.Name));
        }
    }

    // --- 警告 VAL101-VAL109 ---

    /// <summary>VAL101: EnumRef が存在しないenum名を参照</summary>
    /// <summary>VAL103: EnumRef が整数型以外のフィールドに指定されている</summary>
    private static void ValidateEnumRef(
        FieldDefinition field, string structName,
        FormatDefinition format, List<ValidationDiagnostic> diagnostics)
    {
        if (field.EnumRef is null)
            return;

        if (!format.Enums.ContainsKey(field.EnumRef))
        {
            diagnostics.Add(Warning("VAL101",
                $"フィールド '{field.Name}' が参照するenum '{field.EnumRef}' は定義されていません",
                structName, field.Name));
        }

        if (!IsIntegerType(field.Type))
        {
            diagnostics.Add(Warning("VAL103",
                $"フィールド '{field.Name}' ({field.Type}) にenum参照が指定されていますが、enum参照は整数型フィールドでのみ有効です",
                structName, field.Name));
        }
    }

    /// <summary>VAL102: FlagsRef が存在しないflags名を参照</summary>
    /// <summary>VAL104: FlagsRef がascii型・整数型以外のフィールドに指定されている</summary>
    private static void ValidateFlagsRef(
        FieldDefinition field, string structName,
        FormatDefinition format, List<ValidationDiagnostic> diagnostics)
    {
        if (field.FlagsRef is null)
            return;

        if (!format.Flags.ContainsKey(field.FlagsRef))
        {
            diagnostics.Add(Warning("VAL102",
                $"フィールド '{field.Name}' が参照するflags '{field.FlagsRef}' は定義されていません",
                structName, field.Name));
        }

        if (field.Type != FieldType.Ascii && !IsIntegerType(field.Type))
        {
            diagnostics.Add(Warning("VAL104",
                $"フィールド '{field.Name}' ({field.Type}) にflags参照が指定されていますが、flags参照はascii型または整数型フィールドでのみ有効です",
                structName, field.Name));
        }
    }

    /// <summary>VAL106: struct型でないフィールドに StructRef が指定されている</summary>
    private static void ValidateTypeRefCombination(
        FieldDefinition field, string structName,
        List<ValidationDiagnostic> diagnostics)
    {
        if (field.StructRef is not null
            && field.Type is not FieldType.Struct and not FieldType.Switch
            and not FieldType.Zlib and not FieldType.Deflate
            and not FieldType.Gzip and not FieldType.Bzip2 and not FieldType.Lzma
            and not FieldType.Zstd and not FieldType.Lz4)
        {
            diagnostics.Add(Warning("VAL106",
                $"フィールド '{field.Name}' ({field.Type}) にstruct参照が指定されていますが、struct/switch型以外では無視されます",
                structName, field.Name));
        }
    }

    /// <summary>VAL107: 未使用のenum定義</summary>
    private static void ValidateUnusedEnums(
        FormatDefinition format, List<ValidationDiagnostic> diagnostics)
    {
        var usedEnums = new HashSet<string>();
        foreach (var structDef in format.Structs.Values)
        {
            foreach (var field in structDef.Fields)
            {
                if (field.EnumRef is not null)
                    usedEnums.Add(field.EnumRef);
            }
        }

        foreach (var enumName in format.Enums.Keys)
        {
            if (!usedEnums.Contains(enumName))
            {
                diagnostics.Add(Warning("VAL107",
                    $"enum '{enumName}' はどのフィールドからも参照されていません",
                    null, null));
            }
        }
    }

    /// <summary>VAL108: 未使用のflags定義</summary>
    private static void ValidateUnusedFlags(
        FormatDefinition format, List<ValidationDiagnostic> diagnostics)
    {
        var usedFlags = new HashSet<string>();
        foreach (var structDef in format.Structs.Values)
        {
            foreach (var field in structDef.Fields)
            {
                if (field.FlagsRef is not null)
                    usedFlags.Add(field.FlagsRef);
            }
        }

        foreach (var flagsName in format.Flags.Keys)
        {
            if (!usedFlags.Contains(flagsName))
            {
                diagnostics.Add(Warning("VAL108",
                    $"flags '{flagsName}' はどのフィールドからも参照されていません",
                    null, null));
            }
        }
    }

    /// <summary>VAL109: rootから到達不可能なstruct定義</summary>
    private static void ValidateUnreachableStructs(
        FormatDefinition format, List<ValidationDiagnostic> diagnostics)
    {
        var reachable = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(format.RootStruct);
        reachable.Add(format.RootStruct);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!format.Structs.TryGetValue(current, out var structDef))
                continue;

            foreach (var field in structDef.Fields)
            {
                // StructRef
                if (field.StructRef is not null && reachable.Add(field.StructRef))
                    queue.Enqueue(field.StructRef);

                // SwitchCases
                if (field.SwitchCases is not null)
                {
                    foreach (var switchCase in field.SwitchCases)
                    {
                        if (reachable.Add(switchCase.StructRef))
                            queue.Enqueue(switchCase.StructRef);
                    }
                }

                // SwitchDefault
                if (field.SwitchDefault is not null && reachable.Add(field.SwitchDefault))
                    queue.Enqueue(field.SwitchDefault);
            }
        }

        foreach (var structName in format.Structs.Keys)
        {
            if (!reachable.Contains(structName))
            {
                diagnostics.Add(Warning("VAL109",
                    $"struct '{structName}' はルート '{format.RootStruct}' から到達できません",
                    structName, null));
            }
        }
    }

    /// <summary>VAL008: フィールドの align 値が正の整数であること</summary>
    private static void ValidateAlign(
        FieldDefinition field, string structName,
        List<ValidationDiagnostic> diagnostics)
    {
        if (field.Align is { } align && align <= 0)
        {
            diagnostics.Add(Error("VAL008",
                $"フィールド '{field.Name}' の align 値は正の整数が必要です: {align}",
                structName, field.Name));
        }
    }

    /// <summary>VAL009: 構造体の align 値が正の整数であること</summary>
    private static void ValidateStructAlign(
        StructDefinition structDef,
        List<ValidationDiagnostic> diagnostics)
    {
        if (structDef.Align is { } align && align <= 0)
        {
            diagnostics.Add(Error("VAL009",
                $"struct '{structDef.Name}' の align 値は正の整数が必要です: {align}",
                structDef.Name, null));
        }
    }

    /// <summary>VAL110: element_size が繰り返しフィールド以外に指定されている</summary>
    private static void ValidateElementSize(
        FieldDefinition field, string structName,
        List<ValidationDiagnostic> diagnostics)
    {
        if (field.ElementSize is null && field.ElementSizeExpression is null)
            return;

        if (field.Repeat is RepeatMode.None)
        {
            diagnostics.Add(Warning("VAL110",
                $"フィールド '{field.Name}' に element_size が指定されていますが、繰り返しフィールドでのみ有効です",
                structName, field.Name));
        }
    }

    /// <summary>VAL010: virtual型フィールドに value が未指定</summary>
    private static void ValidateVirtualField(
        FieldDefinition field, string structName,
        List<ValidationDiagnostic> diagnostics)
    {
        if (field.Type != FieldType.Virtual)
            return;

        if (field.ValueExpression is null)
        {
            diagnostics.Add(Error("VAL010",
                $"virtual型フィールド '{field.Name}' に value が指定されていません",
                structName, field.Name));
        }
    }

    /// <summary>VAL011: seek_restore が seek なしで指定されている</summary>
    private static void ValidateSeek(
        FieldDefinition field, string structName, List<ValidationDiagnostic> diagnostics)
    {
        if (field.SeekRestore && field.SeekExpression is null)
        {
            diagnostics.Add(Error("VAL011",
                $"フィールド '{field.Name}' に seek_restore が指定されていますが、seek が指定されていません",
                structName, field.Name));
        }

        // VAL015: seek_base without seek
        if (field.SeekBaseExpression is not null && field.SeekExpression is null)
        {
            diagnostics.Add(Error("VAL015",
                $"フィールド '{field.Name}' に seek_base が指定されていますが、seek が指定されていません",
                structName, field.Name));
        }
    }

    /// <summary>VAL012: string_table が整数型以外のフィールドに指定されている</summary>
    private static void ValidateStringTable(
        FieldDefinition field, string structName,
        List<ValidationDiagnostic> diagnostics)
    {
        if (field.StringTableRef is null)
            return;

        if (!IsIntegerType(field.Type))
        {
            diagnostics.Add(Warning("VAL112",
                $"フィールド '{field.Name}' ({field.Type}) にstring_table参照が指定されていますが、string_table参照は整数型フィールドでのみ有効です",
                structName, field.Name));
        }
    }

    /// <summary>VAL014: LengthPrefixedのPrefixSizeが範囲外</summary>
    /// <summary>VAL111: LengthPrefixedがbytes以外の型に指定</summary>
    private static void ValidateLengthPrefixed(
        FieldDefinition field, string structName,
        List<ValidationDiagnostic> diagnostics)
    {
        if (field.Repeat is not RepeatMode.LengthPrefixed lp)
            return;

        if (lp.PrefixSize < 1 || lp.PrefixSize > 4)
        {
            diagnostics.Add(Error("VAL014",
                $"フィールド '{field.Name}' の length_prefix_size は1〜4の範囲が必要です: {lp.PrefixSize}",
                structName, field.Name));
        }

        if (field.Type != FieldType.Bytes)
        {
            diagnostics.Add(Warning("VAL111",
                $"フィールド '{field.Name}' ({field.Type}) に repeat: length_prefixed が指定されていますが、bytes型でのみ有効です",
                structName, field.Name));
        }
    }

    /// <summary>VAL113: 未知のチェックサムアルゴリズム</summary>
    /// <summary>VAL114: 整数系アルゴリズムが非整数フィールドに指定</summary>
    /// <summary>VAL115: ハッシュ系アルゴリズムが非bytesフィールドに指定</summary>
    /// <summary>VAL018: fieldsとrange/rangesが同時指定</summary>
    /// <summary>VAL019: rangeとrangesが同時指定</summary>
    /// <summary>VAL119: exclude_selfがrange/rangesなしで指定</summary>
    private static void ValidateChecksum(
        FieldDefinition field, string structName,
        List<ValidationDiagnostic> diagnostics)
    {
        if (field.Checksum is null)
            return;

        var algorithm = field.Checksum.Algorithm;

        if (!ChecksumAlgorithms.IsKnown(algorithm))
        {
            diagnostics.Add(Warning("VAL113",
                $"フィールド '{field.Name}' のチェックサムアルゴリズム '{algorithm}' は未知です",
                structName, field.Name));
        }

        if (ChecksumAlgorithms.IsIntegerAlgorithm(algorithm) && field.Type == FieldType.Bytes)
        {
            diagnostics.Add(Error("VAL114",
                $"フィールド '{field.Name}' に整数系チェックサムアルゴリズム '{algorithm}' が指定されていますが、bytes型フィールドには使用できません。整数型フィールドを使用してください",
                structName, field.Name));
        }

        if (ChecksumAlgorithms.IsHashAlgorithm(algorithm) && field.Type != FieldType.Bytes)
        {
            diagnostics.Add(Error("VAL115",
                $"フィールド '{field.Name}' にハッシュ系チェックサムアルゴリズム '{algorithm}' が指定されていますが、bytes型以外のフィールドには使用できません",
                structName, field.Name));
        }

        // VAL018: fields（非空）と range/ranges が同時指定
        var hasFields = field.Checksum.FieldNames.Count > 0;
        var hasRange = field.Checksum.Range is not null;
        var hasRanges = field.Checksum.Ranges is { Count: > 0 };
        if (hasFields && (hasRange || hasRanges))
        {
            diagnostics.Add(Error("VAL018",
                $"フィールド '{field.Name}' のチェックサムに fields と range/ranges が同時に指定されています。どちらか一方のみ指定してください",
                structName, field.Name));
        }

        // VAL019: range と ranges が同時指定
        if (hasRange && hasRanges)
        {
            diagnostics.Add(Error("VAL019",
                $"フィールド '{field.Name}' のチェックサムに range と ranges が同時に指定されています。どちらか一方のみ指定してください",
                structName, field.Name));
        }

        // VAL119: exclude_self が range/ranges なしで指定
        if (field.Checksum.ExcludeSelf && !hasRange && !hasRanges)
        {
            diagnostics.Add(Warning("VAL119",
                $"フィールド '{field.Name}' のチェックサムに exclude_self が指定されていますが、range/ranges が指定されていないため無視されます",
                structName, field.Name));
        }
    }

    /// <summary>VAL017: state_if を指定するには state が必要</summary>
    /// <summary>VAL117: state_default を指定するには state が必要</summary>
    private static void ValidateState(
        FieldDefinition field, string structName,
        List<ValidationDiagnostic> diagnostics)
    {
        if (field.StateIf is not null && field.State is null)
        {
            diagnostics.Add(Error("VAL017",
                $"フィールド '{field.Name}' に state_if が指定されていますが、state が指定されていません",
                structName, field.Name));
        }

        if (field.StateDefault.HasValue && field.State is null)
        {
            diagnostics.Add(Warning("VAL117",
                $"フィールド '{field.Name}' に state_default が指定されていますが、state が指定されていません",
                structName, field.Name));
        }
    }

    /// <summary>VAL120: repeat_max が繰り返しフィールド以外に指定されている</summary>
    /// <summary>VAL121: repeat_error_limit が繰り返しフィールド以外に指定されている</summary>
    private static void ValidateRepeatGuard(
        FieldDefinition field, string structName,
        List<ValidationDiagnostic> diagnostics)
    {
        if (field.RepeatMax is not null && field.Repeat is RepeatMode.None)
        {
            diagnostics.Add(Warning("VAL120",
                $"フィールド '{field.Name}' に repeat_max が指定されていますが、繰り返しフィールドでのみ有効です",
                structName, field.Name));
        }

        if (field.RepeatErrorLimit is not null && field.Repeat is RepeatMode.None)
        {
            diagnostics.Add(Warning("VAL121",
                $"フィールド '{field.Name}' に repeat_error_limit が指定されていますが、繰り返しフィールドでのみ有効です",
                structName, field.Name));
        }
    }

    /// <summary>VAL118: ビットストリーム構造体のstruct/switchが非ビットストリーム構造体を参照</summary>
    private static void ValidateBitstreamCrossRef(
        FieldDefinition field, string structName,
        StructDefinition structDef, FormatDefinition format,
        List<ValidationDiagnostic> diagnostics)
    {
        if (!structDef.IsBitstream)
            return;

        // struct フィールド: 参照先が非ビットストリーム → 警告
        if (field.Type == FieldType.Struct && field.StructRef is not null
            && format.Structs.TryGetValue(field.StructRef, out var targetStruct)
            && !targetStruct.IsBitstream)
        {
            diagnostics.Add(Warning("VAL118",
                $"ビットストリーム構造体 '{structName}' のフィールド '{field.Name}' が非ビットストリーム構造体 '{field.StructRef}' を参照しています。" +
                $"ビットストリーム内ではビットストリーム構造体のみ参照することを推奨します",
                structName, field.Name));
        }

        // switch フィールド: 各case・defaultの参照先チェック
        if (field.Type == FieldType.Switch)
        {
            if (field.SwitchCases is not null)
            {
                foreach (var switchCase in field.SwitchCases)
                {
                    if (format.Structs.TryGetValue(switchCase.StructRef, out var caseStruct)
                        && !caseStruct.IsBitstream)
                    {
                        diagnostics.Add(Warning("VAL118",
                            $"ビットストリーム構造体 '{structName}' のswitch '{field.Name}' のcase '{switchCase.StructRef}' が非ビットストリーム構造体です。" +
                            $"ビットストリーム内ではビットストリーム構造体のみ参照することを推奨します",
                            structName, field.Name));
                    }
                }
            }

            if (field.SwitchDefault is not null
                && format.Structs.TryGetValue(field.SwitchDefault, out var defaultStruct)
                && !defaultStruct.IsBitstream)
            {
                diagnostics.Add(Warning("VAL118",
                    $"ビットストリーム構造体 '{structName}' のswitch '{field.Name}' のdefault '{field.SwitchDefault}' が非ビットストリーム構造体です。" +
                    $"ビットストリーム内ではビットストリーム構造体のみ参照することを推奨します",
                    structName, field.Name));
            }
        }
    }

    /// <summary>VAL122: 非ビットストリーム構造体にBitOrderが指定されている</summary>
    private static void ValidateBitOrderOnNonBitstream(
        StructDefinition structDef,
        List<ValidationDiagnostic> diagnostics)
    {
        if (structDef.BitOrder is not null && !structDef.IsBitstream)
        {
            diagnostics.Add(Warning("VAL122",
                $"構造体 '{structDef.Name}' に bit_order が指定されていますが、mode: bitstream ではありません",
                structDef.Name, null));
        }
    }

    // --- ヘルパー ---

    private static bool IsIntegerType(FieldType type) =>
        type is FieldType.UInt8 or FieldType.UInt16 or FieldType.UInt32 or FieldType.UInt64
            or FieldType.Int8 or FieldType.Int16 or FieldType.Int32 or FieldType.Int64
            or FieldType.ULeb128 or FieldType.SLeb128 or FieldType.Vlq;

    private static ValidationDiagnostic Error(string code, string message, string? structName, string? fieldName) =>
        new(DiagnosticSeverity.Error, code, message, structName, fieldName);

    private static ValidationDiagnostic Warning(string code, string message, string? structName, string? fieldName) =>
        new(DiagnosticSeverity.Warning, code, message, structName, fieldName);
}
