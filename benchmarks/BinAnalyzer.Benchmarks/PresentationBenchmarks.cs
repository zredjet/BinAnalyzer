using BenchmarkDotNet.Attributes;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Presentation;

namespace BinAnalyzer.Benchmarks;

/// <summary>
/// GUI が「開く」ときに走る Presentation 層の処理（REQ-177）。
/// <see cref="SyntheticTree"/> で 1 万 / 10 万 / 100 万ノードのツリーを作り、索引・ヘックス行・構造マップ・集計の所要時間とメモリを測る。
/// </summary>
[MemoryDiagnoser]
public class PresentationBenchmarks
{
    [Params(10_000, 100_000, 1_000_000)]
    public int Nodes { get; set; }

    private DecodedStruct _root = null!;
    private byte[] _data = null!;
    private NodeIndex _index = null!;
    private HexRowBuilder _hex = null!;
    private int _midRow;
    private int _midId;

    [GlobalSetup]
    public void Setup()
    {
        (_root, _data) = SyntheticTree.Build(Nodes);
        _index = NodeIndex.Build(_root);
        _hex = new HexRowBuilder(_data, _index);
        _midRow = _hex.RowCount / 2;
        _midId = _index.Count / 2;
    }

    [Benchmark]
    public NodeIndex NodeIndex_Build() => NodeIndex.Build(_root);

    /// <summary>可視範囲相当の 64 行を生成する。1 行あたりの時間は結果を 64 で割る。</summary>
    [Benchmark(OperationsPerInvoke = 64)]
    public int HexRowBuilder_Build64Rows()
    {
        var cells = 0;
        for (var i = 0; i < 64; i++)
            cells += _hex.Build(_midRow + i).Cells.Length;
        return cells;
    }

    [Benchmark]
    public StructureMap StructureMapBuilder_Build() => StructureMapBuilder.Build(_index, _data.Length);

    [Benchmark]
    public ChecksumSummary ChecksumSummary_Compute() => ChecksumSummary.Compute(_index);

    [Benchmark]
    public int NodeSearch_ByName() => NodeSearch.ByName(_root, "checksum").Count;

    [Benchmark]
    public int NodeIndex_FindByPathPattern() => _index.FindByPathPattern("**.checksum").Count;

    /// <summary>パンくず 1 回分: 中央ノードから根までの ElementIndexOf。</summary>
    [Benchmark]
    public int Breadcrumb_Build() => BreadcrumbBuilder.Build(_index.ById(_midId), _index, "root").Count;

    [Benchmark]
    public string NodeIndex_PathOf() => _index.PathOf(_midId);
}
