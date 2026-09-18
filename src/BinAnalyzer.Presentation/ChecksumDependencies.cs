using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Patching;

namespace BinAnalyzer.Presentation;

/// <summary>編集範囲を算出対象に含むチェックサムフィールド。</summary>
public sealed record ChecksumDependency(int NodeId, string Path, string Algorithm, bool? Valid);

/// <summary>「この範囲を書き換えると再計算が必要になるチェックサム」を <see cref="DecodedInteger.ChecksumCoverage"/> から求める。</summary>
public static class ChecksumDependencies
{
    public static IReadOnlyList<ChecksumDependency> Covering(NodeIndex index, ByteRange range, int excludeId = -1)
    {
        var result = new List<ChecksumDependency>();
        if (range.IsEmpty)
            return result;
        for (var id = 1; id < index.Count; id++)
        {
            if (id == excludeId || !index.IsInFileSpace(id))
                continue;
            var node = index.ById(id);
            var (coverage, algorithm, valid) = node switch
            {
                DecodedInteger i => (i.ChecksumCoverage, i.ChecksumAlgorithm, i.ChecksumValid),
                DecodedBytes b => (b.ChecksumCoverage, b.ChecksumAlgorithm, b.ChecksumValid),
                _ => (null, null, null),
            };
            if (coverage is null || !coverage.Any(c => c.Overlaps(range)))
                continue;
            result.Add(new ChecksumDependency(id, index.PathOf(id), algorithm ?? "", valid));
        }
        return result;
    }
}
