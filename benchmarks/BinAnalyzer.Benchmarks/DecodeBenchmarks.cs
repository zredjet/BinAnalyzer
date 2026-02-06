using BenchmarkDotNet.Attributes;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;

namespace BinAnalyzer.Benchmarks;

[MemoryDiagnoser]
public class DecodeBenchmarks
{
    private byte[] _miniPngData = null!;
    private FormatDefinition _pngFormat = null!;
    private byte[] _simpleData = null!;
    private FormatDefinition _simpleFormat = null!;

    [GlobalSetup]
    public void Setup()
    {
        var pngPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "png.bdef.yaml");
        if (File.Exists(pngPath))
        {
            _pngFormat = new YamlFormatLoader().Load(pngPath);
            _miniPngData = CreateMinimalPng();
        }

        // Simple format for fast benchmarking even without PNG format file
        _simpleFormat = new FormatDefinition
        {
            Name = "Simple",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["main"] = new()
                {
                    Name = "main",
                    Fields =
                    [
                        new FieldDefinition { Name = "a", Type = FieldType.UInt32 },
                        new FieldDefinition { Name = "b", Type = FieldType.UInt16 },
                        new FieldDefinition { Name = "c", Type = FieldType.UInt8 },
                        new FieldDefinition { Name = "d", Type = FieldType.Bytes, Size = 4 },
                        new FieldDefinition { Name = "e", Type = FieldType.Ascii, Size = 4 },
                    ],
                },
            },
            RootStruct = "main",
        };
        _simpleData = new byte[15]; // 4+2+1+4+4
        "TEST"u8.CopyTo(_simpleData.AsSpan(11));
    }

    [Benchmark]
    public object DecodeSimpleStruct()
    {
        return new BinaryDecoder().Decode(_simpleData, _simpleFormat);
    }

    [Benchmark]
    public object? DecodeMinimalPng()
    {
        if (_pngFormat is null) return null;
        return new BinaryDecoder().Decode(_miniPngData, _pngFormat);
    }

    private static byte[] CreateMinimalPng()
    {
        using var ms = new MemoryStream();
        // PNG signature
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        // IHDR chunk
        WriteChunk(ms, "IHDR"u8, [
            0x00, 0x00, 0x00, 0x01, // width=1
            0x00, 0x00, 0x00, 0x01, // height=1
            0x08,                   // bit_depth=8
            0x02,                   // color_type=2 (truecolor)
            0x00,                   // compression=0
            0x00,                   // filter=0
            0x00,                   // interlace=0
        ]);

        // IDAT chunk (minimal compressed data)
        WriteChunk(ms, "IDAT"u8, [0x78, 0x01, 0x62, 0x64, 0x60, 0x60, 0x00, 0x00, 0x00, 0x07, 0x00, 0x01]);

        // IEND chunk
        WriteChunk(ms, "IEND"u8, []);

        return ms.ToArray();
    }

    private static void WriteChunk(MemoryStream ms, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var len = new byte[4];
        len[0] = (byte)(data.Length >> 24);
        len[1] = (byte)(data.Length >> 16);
        len[2] = (byte)(data.Length >> 8);
        len[3] = (byte)(data.Length);
        ms.Write(len);
        ms.Write(type);
        ms.Write(data);
        ms.Write(new byte[4]); // CRC placeholder
    }
}
