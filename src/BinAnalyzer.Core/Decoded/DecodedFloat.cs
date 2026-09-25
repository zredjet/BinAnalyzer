using BinAnalyzer.Core.Models;

namespace BinAnalyzer.Core.Decoded;

/// <summary>浮動小数点数の精度（値はバイト数）。</summary>
public enum FloatPrecision
{
    /// <summary>IEEE 754 binary16（float16、REQ-200）。</summary>
    Half = 2,
    /// <summary>IEEE 754 binary32（float32）。</summary>
    Single = 4,
    /// <summary>IEEE 754 binary64（float64）。</summary>
    Double = 8,
}

public sealed class DecodedFloat : DecodedNode
{
    /// <summary>値（float16 / float32 も double に広げる。無限大・NaN・非正規化数を含む）。</summary>
    public required double Value { get; init; }

    public required FloatPrecision Precision { get; init; }

    /// <summary>精度の型名（<c>float16</c> / <c>float32</c> / <c>float64</c>）。</summary>
    public string FloatTypeName => Precision switch
    {
        FloatPrecision.Half => "float16",
        FloatPrecision.Single => "float32",
        _ => "float64",
    };

    /// <summary>デコード時の実効エンディアン（書き戻しに使う）。</summary>
    public Endianness? Endianness { get; init; }
}
