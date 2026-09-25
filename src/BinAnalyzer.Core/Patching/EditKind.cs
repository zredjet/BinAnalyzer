using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;

namespace BinAnalyzer.Core.Patching;

/// <summary>値編集の入力方式。<see cref="None"/> は編集不可。</summary>
public enum EditKind
{
    None,
    Integer,
    /// <summary>enum 参照付き整数。選択肢から選ぶ。</summary>
    Enum,
    Float,
    /// <summary>固定長文字列</summary>
    String,
    /// <summary>固定長バイト列（16 進入力）</summary>
    Bytes,
}

/// <summary>
/// ノード単体から決まる編集可否。データ空間（圧縮ストリーム内か）はツリー索引が必要なので Presentation 側で判定する。
/// エンコーダ（Engine）と編集 UI（Presentation / GUI）で同じ規則を使う。
/// </summary>
public static class FieldEditRules
{
    public static EditKind Classify(DecodedNode node, out string? reason)
    {
        reason = null;
        switch (node)
        {
            case DecodedInteger i:
                if (i.BitOffset is not null)
                {
                    reason = "ビットストリームのフィールドは編集できません";
                    return EditKind.None;
                }
                if (i.DslType is null)
                {
                    reason = "型情報が無いため編集できません";
                    return EditKind.None;
                }
                if (!IsFixedInteger(i.DslType.Value))
                {
                    reason = "可変長整数（LEB128 / VLQ）はサイズが変わるため編集できません";
                    return EditKind.None;
                }
                if (i.Size is not (1 or 2 or 4 or 8))
                {
                    reason = $"サイズ {i.Size} B の整数は編集できません";
                    return EditKind.None;
                }
                if (i.Size > 1 && i.Endianness is null)
                {
                    reason = "エンディアン情報が無いため編集できません";
                    return EditKind.None;
                }
                return i.EnumRef is not null ? EditKind.Enum : EditKind.Integer;

            case DecodedFloat f:
                if (f.BitOffset is not null)
                {
                    reason = "ビットストリームのフィールドは編集できません";
                    return EditKind.None;
                }
                if (f.Size != (int)f.Precision || f.Endianness is null)
                {
                    reason = "エンディアン情報が無いため編集できません";
                    return EditKind.None;
                }
                return EditKind.Float;

            case DecodedString s:
                if (s.BitOffset is not null)
                {
                    reason = "ビットストリームのフィールドは編集できません";
                    return EditKind.None;
                }
                if (s.Encoding is "asciiz" or "utf8z" || s.DslType is FieldType.AsciiZ or FieldType.Utf8Z)
                {
                    reason = "NUL 終端文字列はサイズが変わるため編集できません";
                    return EditKind.None;
                }
                if (!IsFixedStringEncoding(s.Encoding))
                {
                    reason = $"エンコーディング {s.Encoding} は編集に対応していません";
                    return EditKind.None;
                }
                if (s.Size <= 0)
                {
                    reason = "サイズ 0 のフィールドは編集できません";
                    return EditKind.None;
                }
                return EditKind.String;

            case DecodedBytes b:
                if (b.Size <= 0)
                {
                    reason = "サイズ 0 のフィールドは編集できません";
                    return EditKind.None;
                }
                return EditKind.Bytes;

            case DecodedStruct or DecodedArray:
                reason = "構造体・配列は要素ごとに編集してください";
                return EditKind.None;

            case DecodedCompressed:
                reason = "圧縮データの編集（再圧縮）は未対応です";
                return EditKind.None;

            case DecodedVirtual:
                reason = "仮想フィールドは計算値なので編集できません";
                return EditKind.None;

            default:
                reason = "この種別のフィールドは編集できません";
                return EditKind.None;
        }
    }

    public static bool IsFixedInteger(FieldType type) => type is
        FieldType.UInt8 or FieldType.UInt16 or FieldType.UInt32 or FieldType.UInt64 or
        FieldType.Int8 or FieldType.Int16 or FieldType.Int32 or FieldType.Int64;

    public static bool IsSignedInteger(FieldType type) => type is
        FieldType.Int8 or FieldType.Int16 or FieldType.Int32 or FieldType.Int64;

    public static bool IsFixedStringEncoding(string encoding) => encoding.ToLowerInvariant() is
        "ascii" or "utf8" or "utf-8" or "utf16le" or "utf16be" or "sjis" or "shift_jis" or "latin1";
}
