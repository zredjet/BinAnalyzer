using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Presentation;

public sealed record ChecksumItem(int NodeId, string Path, bool Valid);

/// <summary>InfoBar 用の集計: チェックサム・検証式・圧縮ストリーム・エラー。</summary>
public sealed record ChecksumSummary(
    int ChecksumTotal,
    int ChecksumValid,
    IReadOnlyList<ChecksumItem> Checksums,
    int ValidationTotal,
    int ValidationPassed,
    int CompressedStreams,
    int Errors)
{
    public bool AllChecksumsValid => ChecksumValid == ChecksumTotal;
    public bool AllValidationsPassed => ValidationPassed == ValidationTotal;

    public static ChecksumSummary Compute(NodeIndex index)
    {
        var checksums = new List<ChecksumItem>();
        int vTotal = 0, vPassed = 0, zips = 0, errors = 0;
        var id = -1;
        foreach (var node in index.Nodes)
        {
            id++;
            bool? cs = node switch
            {
                DecodedInteger i => i.ChecksumValid,
                DecodedBytes b => b.ChecksumValid,
                _ => null,
            };
            if (cs is { } v)
                checksums.Add(new ChecksumItem(id, index.PathOf(id), v));
            if (node.ValidationPassed is { } vp)
            {
                vTotal++;
                if (vp) vPassed++;
            }
            if (node is DecodedCompressed) zips++;
            if (node is DecodedError) errors++;
        }
        return new ChecksumSummary(checksums.Count, checksums.Count(c => c.Valid), checksums, vTotal, vPassed, zips, errors);
    }
}
