using BenchmarkDotNet.Attributes;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Output;

namespace BinAnalyzer.Benchmarks;

[MemoryDiagnoser]
public class OutputBenchmarks
{
    private DecodedStruct _decoded = null!;
    private byte[] _rawData = null!;

    [GlobalSetup]
    public void Setup()
    {
        _decoded = new DecodedStruct
        {
            Name = "test", StructType = "test", Offset = 0, Size = 30,
            Children =
            [
                new DecodedInteger { Name = "magic", Offset = 0, Size = 4, Value = 0x89504E47 },
                new DecodedString { Name = "type", Offset = 4, Size = 4, Value = "IHDR", Encoding = "ascii" },
                new DecodedStruct
                {
                    Name = "header", StructType = "header", Offset = 8, Size = 13,
                    Children =
                    [
                        new DecodedInteger { Name = "width", Offset = 8, Size = 4, Value = 1920 },
                        new DecodedInteger { Name = "height", Offset = 12, Size = 4, Value = 1080 },
                        new DecodedInteger { Name = "bit_depth", Offset = 16, Size = 1, Value = 8 },
                        new DecodedInteger { Name = "color_type", Offset = 17, Size = 1, Value = 2, EnumLabel = "truecolor" },
                        new DecodedInteger { Name = "compression", Offset = 18, Size = 1, Value = 0, EnumLabel = "deflate" },
                    ],
                },
                new DecodedBytes { Name = "data", Offset = 21, Size = 8, RawBytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 } },
            ],
        };
        _rawData = new byte[30];
    }

    [Benchmark]
    public string FormatTree()
    {
        return new TreeOutputFormatter().Format(_decoded);
    }

    [Benchmark]
    public string FormatJson()
    {
        return new JsonOutputFormatter().Format(_decoded);
    }

    [Benchmark]
    public string FormatHexdump()
    {
        return new HexDumpOutputFormatter().Format(_decoded, _rawData);
    }

    [Benchmark]
    public string FormatHtml()
    {
        return new HtmlOutputFormatter().Format(_decoded);
    }

    [Benchmark]
    public string FormatMap()
    {
        return new MapOutputFormatter().Format(_decoded, _rawData);
    }
}
