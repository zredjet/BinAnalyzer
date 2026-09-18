using System.Diagnostics;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;

namespace BinAnalyzer.Benchmarks;

/// <summary>
/// 実ファイルを 1 回デコードして、所要時間・割り当てバイト数・木の保持メモリをノード数あたりで出す（REQ-180）。
/// BenchmarkDotNet の Allocated はノード数が分からないので、こちらで補う。
/// <code>dotnet run -c Release --project benchmarks/BinAnalyzer.Benchmarks -- probe file.pcap formats/pcap.bdef.yaml</code>
/// </summary>
public static class DecodeMemoryProbe
{
    public static int Run(string file, string formatPath)
    {
        var format = new YamlFormatLoader().Load(formatPath);
        var data = File.ReadAllBytes(file);

        // JIT ウォームアップ（先頭 4 KB をエラー継続で）
        new BinaryDecoder().DecodeWithRecovery(data.AsMemory(0, Math.Min(data.Length, 4096)), format, ErrorMode.Continue);
        FullCollect();

        var (ms, nodes, allocated, alive, byType) = DecodeOnce(data, format);
        // DecodeOnce から戻れば木は到達不能になる。その前後の差が木の保持メモリ
        FullCollect();
        var retained = alive - GC.GetTotalMemory(true);

        Console.WriteLine($"{Path.GetFileName(file)}: {data.Length:N0} bytes, {nodes:N0} nodes");
        Console.WriteLine($"  decode      : {ms} ms ({ms * 1000.0 / nodes:F2} us/node)");
        Console.WriteLine($"  allocated   : {allocated / 1048576.0:F1} MB ({allocated / (double)nodes:F0} B/node)");
        // 木を捨てて全 GC した前後のヒープ差。断片化や世代の再配置を含むので、ノード実サイズ × 個数より大きめに出る（上限の目安）
        Console.WriteLine($"  heap delta  : {retained / 1048576.0:F1} MB ({retained / (double)nodes:F0} B/node, 木を解放した前後の差 = 保持メモリの上限)");
        Console.WriteLine("  nodes by type: " + string.Join(", ", byType.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value:N0}")));
        return 0;
    }

    private static (long Ms, int Nodes, long Allocated, long Alive, Dictionary<string, int> ByType) DecodeOnce(byte[] data, FormatDefinition format)
    {
        var allocBefore = GC.GetTotalAllocatedBytes(true);
        var sw = Stopwatch.StartNew();
        var root = new BinaryDecoder().Decode(data, format);
        sw.Stop();
        var allocated = GC.GetTotalAllocatedBytes(true) - allocBefore;
        var byType = new Dictionary<string, int>();
        var nodes = Count(root, byType);
        FullCollect();
        var alive = GC.GetTotalMemory(true);
        GC.KeepAlive(root);
        return (sw.ElapsedMilliseconds, nodes, allocated, alive, byType);
    }

    private static int Count(DecodedNode n, Dictionary<string, int> byType)
    {
        byType[n.GetType().Name] = byType.GetValueOrDefault(n.GetType().Name) + 1;
        return 1 + n switch
        {
            DecodedStruct s => s.Children.Sum(c => Count(c, byType)),
            DecodedArray a => a.Elements.Sum(e => Count(e, byType)),
            DecodedCompressed { DecodedContent: { } c } => Count(c, byType),
            _ => 0,
        };
    }

    private static void FullCollect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
