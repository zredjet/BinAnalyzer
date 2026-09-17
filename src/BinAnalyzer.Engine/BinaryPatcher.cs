using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Patching;

namespace BinAnalyzer.Engine;

/// <summary>再計算して書き戻したチェックサムフィールド。</summary>
public sealed record ChecksumFix(string Path, ByteRange Range, string Algorithm, byte[] Before, byte[] After);

/// <summary>
/// パッチ適用の結果。<see cref="Data"/> は入力とは別の新しい配列。
/// <see cref="Converged"/> が false なら、上限回数内にチェックサムの連鎖が収まらなかった（または再デコードに失敗した）。
/// </summary>
public sealed record PatchOutcome(byte[] Data, IReadOnlyList<ChecksumFix> ChecksumFixes, bool Converged);

/// <summary>
/// 固定長パッチを適用し、変更範囲を算出対象に含むチェックサム（<c>checksum:</c> 定義）を再計算して書き戻す。
/// 再計算はエンジンの検証結果（<see cref="DecodedInteger.ChecksumExpected"/> / <see cref="DecodedBytes.ChecksumExpectedHex"/>）を
/// そのまま使い、チェックサムが別のチェックサムの範囲に含まれる場合は変化が無くなるまで反復する。
/// </summary>
public static class BinaryPatcher
{
    public const int MaxChecksumPasses = 8;

    public static PatchOutcome Apply(
        ReadOnlySpan<byte> data,
        FormatDefinition format,
        IReadOnlyList<BytePatch> patches,
        DecodeOptions? options = null,
        bool recalculateChecksums = true)
    {
        var buffer = data.ToArray();
        var explicitRanges = new List<ByteRange>(patches.Count);
        foreach (var patch in patches)
        {
            if (patch.Offset < 0 || patch.Offset + patch.Bytes.Length > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(patches),
                    $"パッチ範囲 [{patch.Offset}..{patch.Offset + patch.Bytes.Length}) がデータ長 {buffer.Length} を超えています");
            patch.Bytes.CopyTo(buffer, (int)patch.Offset);
            explicitRanges.Add(patch.Range);
        }

        var fixes = new List<ChecksumFix>();
        if (!recalculateChecksums || explicitRanges.Count == 0)
            return new PatchOutcome(buffer, fixes, true);

        var changed = new List<ByteRange>(explicitRanges);
        for (var pass = 0; pass < MaxChecksumPasses; pass++)
        {
            DecodedStruct root;
            try
            {
                root = new BinaryDecoder().DecodeWithRecovery(buffer, format, ErrorMode.Continue, options).Root;
            }
            catch (Exception)
            {
                // 構造が壊れて再デコードできない場合は、パッチ本体だけ適用した状態で返す
                return new PatchOutcome(buffer, fixes, false);
            }

            var passFixes = new List<ChecksumFix>();
            Walk(root, "", (node, path) =>
            {
                if (TryBuildFix(node, path, buffer, changed, explicitRanges) is { } fix)
                    passFixes.Add(fix);
            });
            if (passFixes.Count == 0)
                return new PatchOutcome(buffer, fixes, true);

            foreach (var fix in passFixes)
            {
                fix.After.CopyTo(buffer, (int)fix.Range.Offset);
                changed.Add(fix.Range);
                fixes.Add(fix);
            }
        }
        return new PatchOutcome(buffer, fixes, false);
    }

    /// <summary>ファイル空間のノードだけを、Presentation の NodeIndex と同じパス規則（<c>a.b[0].c</c>）で走査する。</summary>
    private static void Walk(DecodedNode node, string path, Action<DecodedNode, string> visit)
    {
        visit(node, path);
        switch (node)
        {
            case DecodedStruct st:
                foreach (var child in st.Children)
                    Walk(child, path.Length == 0 ? child.Name : $"{path}.{child.Name}", visit);
                break;
            case DecodedArray arr:
                for (var i = 0; i < arr.Elements.Count; i++)
                    Walk(arr.Elements[i], $"{path}[{i}]", visit);
                break;
            // DecodedCompressed の展開内容はストリーム空間なので辿らない
        }
    }

    private static ChecksumFix? TryBuildFix(
        DecodedNode node, string path, byte[] buffer,
        List<ByteRange> changed, List<ByteRange> explicitRanges)
    {
        IReadOnlyList<ByteRange>? coverage;
        byte[]? after;
        string? algorithm;
        switch (node)
        {
            case DecodedInteger { ChecksumCoverage: { } cov, ChecksumValid: false, ChecksumExpected: { } expected, BitOffset: null } i:
                coverage = cov;
                algorithm = i.ChecksumAlgorithm ?? "";
                after = EncodeUnsigned((ulong)expected, (int)i.Size, i.Endianness ?? Endianness.Big);
                break;
            case DecodedBytes { ChecksumCoverage: { } cov, ChecksumValid: false, ChecksumExpectedHex: { } hex } b:
                coverage = cov;
                algorithm = b.ChecksumAlgorithm ?? "";
                after = Convert.FromHexString(hex);
                if (after.Length != b.Size) return null;
                break;
            default:
                return null;
        }

        var self = new ByteRange(node.Offset, node.Size);
        if (self.IsEmpty || self.End > buffer.Length)
            return null;
        // 利用者がチェックサム欄そのものを書き換えた場合はその値を尊重する
        if (explicitRanges.Any(r => r.Overlaps(self)))
            return null;
        if (!coverage.Any(c => changed.Any(c.Overlaps)))
            return null;

        var before = buffer.AsSpan((int)self.Offset, (int)self.Size).ToArray();
        if (before.AsSpan().SequenceEqual(after))
            return null;
        return new ChecksumFix(path, self, algorithm, before, after);
    }

    /// <summary>符号なし整数を指定サイズ・エンディアンで並べる（上位の余りは切り捨て）。</summary>
    public static byte[] EncodeUnsigned(ulong value, int size, Endianness endianness)
    {
        var bytes = new byte[size];
        for (var i = 0; i < size; i++)
        {
            var b = (byte)(value >> (8 * i));
            if (endianness == Endianness.Little) bytes[i] = b; else bytes[size - 1 - i] = b;
        }
        return bytes;
    }
}
