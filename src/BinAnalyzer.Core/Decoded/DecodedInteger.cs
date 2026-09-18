using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Patching;

namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedInteger : DecodedNode
{
    public required long Value { get; init; }

    /// <summary>デコード時の実効エンディアン（書き戻しに使う）。ビットストリーム / 可変長整数では意味を持たない。</summary>
    public Endianness? Endianness { get; init; }

    // enum / チェックサム / 文字列テーブルは整数ノードの 1 割程度しか使わないので、使うノードだけが持つ補助オブジェクトにまとめる
    // （REQ-180: 168 B → 104 B / ノード）。プロパティの見え方は変えない。null を設定しても補助オブジェクトは作らない
    // （デコーダは全プロパティを無条件に初期化するため）。
    private Extras? _extras;

    public string? EnumLabel
    {
        get => _extras?.EnumLabel;
        init { if (value is not null) (_extras ??= new()).EnumLabel = value; else if (_extras is not null) _extras.EnumLabel = null; }
    }

    public string? EnumDescription
    {
        get => _extras?.EnumDescription;
        init { if (value is not null) (_extras ??= new()).EnumDescription = value; else if (_extras is not null) _extras.EnumDescription = null; }
    }

    public bool? ChecksumValid
    {
        get => _extras?.ChecksumValid;
        init { if (value is not null) (_extras ??= new()).ChecksumValid = value; else if (_extras is not null) _extras.ChecksumValid = null; }
    }

    public long? ChecksumExpected
    {
        get => _extras?.ChecksumExpected;
        init { if (value is not null) (_extras ??= new()).ChecksumExpected = value; else if (_extras is not null) _extras.ChecksumExpected = null; }
    }

    public string? ChecksumAlgorithm
    {
        get => _extras?.ChecksumAlgorithm;
        init { if (value is not null) (_extras ??= new()).ChecksumAlgorithm = value; else if (_extras is not null) _extras.ChecksumAlgorithm = null; }
    }

    public string? StringTableValue
    {
        get => _extras?.StringTableValue;
        init { if (value is not null) (_extras ??= new()).StringTableValue = value; else if (_extras is not null) _extras.StringTableValue = null; }
    }

    /// <summary>参照している enum 定義名（編集時の選択肢に使う）。</summary>
    public string? EnumRef
    {
        get => _extras?.EnumRef;
        init { if (value is not null) (_extras ??= new()).EnumRef = value; else if (_extras is not null) _extras.EnumRef = null; }
    }

    /// <summary>
    /// チェックサムフィールドの場合、検証時に算出対象とした（ノードと同じデータ空間の）バイト範囲。
    /// 編集範囲との重なりで「再計算が必要か」を判定する。チェックサムでなければ null。
    /// </summary>
    public IReadOnlyList<ByteRange>? ChecksumCoverage
    {
        get => _extras?.ChecksumCoverage;
        init { if (value is not null) (_extras ??= new()).ChecksumCoverage = value; else if (_extras is not null) _extras.ChecksumCoverage = null; }
    }

    private sealed class Extras
    {
        public string? EnumLabel;
        public string? EnumDescription;
        public bool? ChecksumValid;
        public long? ChecksumExpected;
        public string? ChecksumAlgorithm;
        public string? StringTableValue;
        public string? EnumRef;
        public IReadOnlyList<ByteRange>? ChecksumCoverage;
    }
}
