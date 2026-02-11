using BenchmarkDotNet.Attributes;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;

namespace BinAnalyzer.Benchmarks;

[MemoryDiagnoser]
public class YamlLoadBenchmarks
{
    private string[] _formatPaths = null!;
    private string _pngPath = null!;
    private string _elfPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        var formatsDir = DecodeBenchmarks.FindFormatsDirectory();
        _formatPaths = Directory.GetFiles(formatsDir, "*.bdef.yaml");
        _pngPath = _formatPaths.First(p => p.Contains("png"));
        _elfPath = _formatPaths.First(p => p.Contains("elf"));
    }

    [Benchmark]
    public FormatDefinition[] LoadAllFormats()
    {
        var loader = new YamlFormatLoader();
        return _formatPaths.Select(p => loader.Load(p)).ToArray();
    }

    [Benchmark]
    public FormatDefinition LoadSingleFormat_Png()
    {
        return new YamlFormatLoader().Load(_pngPath);
    }

    [Benchmark]
    public FormatDefinition LoadSingleFormat_Elf()
    {
        return new YamlFormatLoader().Load(_elfPath);
    }
}
