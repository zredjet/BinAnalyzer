namespace BinAnalyzer.Core.Patching;

/// <summary>
/// 値 → バイト列のエンコード結果。<see cref="Bytes"/> が null ならエラーで、<see cref="Error"/> に理由が入る。
/// <see cref="Note"/> は成功時の補足（例: 不足分を 0x00 で埋めた）。
/// </summary>
public sealed record FieldEncodeResult(byte[]? Bytes, string? Error, string? Note = null)
{
    public bool IsSuccess => Bytes is not null;

    public static FieldEncodeResult Ok(byte[] bytes, string? note = null) => new(bytes, null, note);
    public static FieldEncodeResult Fail(string error) => new(null, error);
}
