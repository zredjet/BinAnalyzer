namespace BinAnalyzer.Core.Models;

/// <summary><see cref="FieldType"/> の分類。複数の層で同じ判定を書かないための単一の情報源。</summary>
public static class FieldTypeCategories
{
    /// <summary>圧縮データ型（展開してからネスト解析する型）か。</summary>
    public static bool IsCompressed(FieldType type) =>
        type is FieldType.Zlib or FieldType.Deflate or FieldType.Gzip
            or FieldType.Bzip2 or FieldType.Lzma or FieldType.Zstd or FieldType.Lz4;
}
