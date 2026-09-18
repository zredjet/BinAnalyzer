using BinAnalyzer.Core.Patching;

namespace BinAnalyzer.Presentation;

/// <summary>編集可否と、不可の場合の理由。<see cref="MayShiftLayout"/> は長さ系フィールド（変更で後続オフセットが動く可能性）。</summary>
public sealed record EditabilityInfo(EditKind Kind, string? Reason, bool MayShiftLayout = false)
{
    public bool CanEdit => Kind != EditKind.None;
}

/// <summary>ツリー索引を使った編集可否の導出。ノード単体の規則は <see cref="FieldEditRules"/>。</summary>
public static class FieldEditability
{
    public static EditabilityInfo Of(NodeIndex index, int id)
    {
        if (id <= 0 || id >= index.Count)
            return new EditabilityInfo(EditKind.None, "ルートは編集できません");
        if (!index.IsInFileSpace(id))
            return new EditabilityInfo(EditKind.None, "圧縮ストリーム内のフィールドは編集できません（再圧縮は未対応）");

        var node = index.ById(id);
        var kind = FieldEditRules.Classify(node, out var reason);
        return new EditabilityInfo(kind, reason, kind != EditKind.None && index.KindOf(id) == FieldKind.Len);
    }
}
