using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Engine;

/// <summary>
/// 式評価から struct / array の値を「デコード結果ノードそのもの」として参照するための変換。
/// 以前は struct フィールドごとに <c>Dictionary&lt;string, object&gt;</c> を（入れ子を含めて再帰的に）作って変数に束縛していたが、
/// それがデコード中の割り当ての 6 割を占めていた（REQ-180）。ノードは不変なので、必要なときに子を名前で引けば同じ結果になる。
/// </summary>
internal static class NodeValues
{
    /// <summary>struct のメンバー値。同名の子が複数あれば後のもの（辞書に上書きしていた挙動と同じ）。無ければ null。</summary>
    public static object? Member(DecodedStruct st, string name)
    {
        var children = st.Children;
        for (var i = children.Count - 1; i >= 0; i--)
        {
            var child = children[i];
            if (child.Name == name)
            {
                var value = Of(child);
                if (value is not null)
                    return value;
            }
            // bitfield のサブフィールドも struct のメンバーとして見える
            if (child is DecodedBitfield bf)
            {
                var fields = bf.Fields;
                for (var j = fields.Count - 1; j >= 0; j--)
                    if (fields[j].Name == name)
                        return BoxCache.Box(fields[j].Value);
            }
        }
        return null;
    }

    /// <summary>ノードを式の値にする。値を持たないノード（bytes / compressed / error 等）は null。</summary>
    public static object? Of(DecodedNode node) => node switch
    {
        DecodedInteger di => BoxCache.Box(di.Value),
        DecodedString ds => ds.Value,
        DecodedFloat df => df.Value,
        DecodedVirtual dv => dv.Value,
        DecodedBitfield bf => BoxCache.Box(bf.RawValue),
        DecodedStruct st => st,
        DecodedArray arr => IsValueArray(arr) ? arr : null,
        _ => null,
    };

    /// <summary>全要素が式の値に変換できる配列か（以前は変換できない要素があると配列ごと変数にしなかった）。</summary>
    public static bool IsValueArray(DecodedArray arr)
    {
        var elements = arr.Elements;
        if (elements.Count == 0)
            return false;
        for (var i = 0; i < elements.Count; i++)
        {
            if (elements[i] is not (DecodedInteger or DecodedString or DecodedFloat or DecodedStruct))
                return false;
        }
        return true;
    }

    public static object Element(DecodedArray arr, int index) => Of(arr.Elements[index])
        ?? throw new InvalidOperationException($"Array element {index} of '{arr.Name}' has no value");
}
