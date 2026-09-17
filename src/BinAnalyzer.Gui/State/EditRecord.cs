using BinAnalyzer.Core.Patching;
using BinAnalyzer.Engine;
using BinAnalyzer.Presentation;

namespace BinAnalyzer.Gui.State;

/// <summary>1 箇所の同一長バイト書き込み。Undo は <see cref="Before"/>、Redo は <see cref="After"/> を戻す。</summary>
public sealed record ByteWrite(long Offset, byte[] Before, byte[] After);

/// <summary>1 回の「適用」= フィールド本体の書き込み ＋ 再計算したチェックサムの書き込み。</summary>
public sealed record EditRecord(string Path, string Summary, IReadOnlyList<ByteWrite> Writes, IReadOnlyList<ChecksumFix> ChecksumFixes)
{
    public long ChangedBytes => Writes.Sum(w => (long)w.After.Length);
}

/// <summary>入力中のプレビュー。<see cref="Bytes"/> が null ならエラー（<see cref="Error"/>）か編集不可。</summary>
public sealed record EditPreview(
    EditabilityInfo Editability,
    byte[]? Bytes,
    string? Error,
    string? Note,
    bool Unchanged,
    IReadOnlyList<ChecksumDependency> Dependencies)
{
    public bool CanApply => Bytes is not null && !Unchanged;
}

/// <summary>適用結果。</summary>
public sealed record EditOutcome(bool Success, string? Error, EditRecord? Record)
{
    public static EditOutcome Fail(string error) => new(false, error, null);
}
