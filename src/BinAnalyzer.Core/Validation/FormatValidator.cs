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
                ValidateEnumRef(field, structName, format, diagnostics);
                ValidateFlagsRef(field, structName, format, diagnostics);
                ValidateTypeRefCombination(field, structName, diagnostics);
                ValidateAlign(field, structName, diagnostics);
            }

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

    /// <summary>VAL007: サイズ指定が必要な型でサイズ未指定</summary>
    private static void ValidateSizeSpec(
        FieldDefinition field, string structName,
        List<ValidationDiagnostic> diagnostics)
    {
        if (field.Type is FieldType.Bytes or FieldType.Ascii or FieldType.Utf8
                or FieldType.Utf16Le or FieldType.Utf16Be or FieldType.ShiftJis or FieldType.Latin1
                or FieldType.Bitfield or FieldType.Zlib or FieldType.Deflate)
        {
            if (!field.Size.HasValue && field.SizeExpression is null && !field.SizeRemaining)
            {
                diagnostics.Add(Error("VAL007",
                    $"フィールド '{field.Name}' ({field.Type}) にサイズ指定がありません（size, size式, remaining のいずれかが必要です）",
                    structName, field.Name));
            }
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
    /// <summary>VAL104: FlagsRef がascii型以外のフィールドに指定されている</summary>
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

        if (field.Type != FieldType.Ascii)
        {
            diagnostics.Add(Warning("VAL104",
                $"フィールド '{field.Name}' ({field.Type}) にflags参照が指定されていますが、flags参照はascii型フィールドでのみ有効です",
                structName, field.Name));
        }
    }

    /// <summary>VAL106: struct型でないフィールドに StructRef が指定されている</summary>
    private static void ValidateTypeRefCombination(
        FieldDefinition field, string structName,
        List<ValidationDiagnostic> diagnostics)
    {
        if (field.StructRef is not null && field.Type is not FieldType.Struct and not FieldType.Switch)
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

    // --- ヘルパー ---

    private static bool IsIntegerType(FieldType type) =>
        type is FieldType.UInt8 or FieldType.UInt16 or FieldType.UInt32 or FieldType.UInt64
            or FieldType.Int8 or FieldType.Int16 or FieldType.Int32 or FieldType.Int64;

    private static ValidationDiagnostic Error(string code, string message, string? structName, string? fieldName) =>
        new(DiagnosticSeverity.Error, code, message, structName, fieldName);

    private static ValidationDiagnostic Warning(string code, string message, string? structName, string? fieldName) =>
        new(DiagnosticSeverity.Warning, code, message, structName, fieldName);
}
