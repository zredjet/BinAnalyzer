using System.Buffers.Binary;
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
    private byte[] _mediumPngData = null!;
    private byte[] _largePngData = null!;
    private byte[] _mediumPcapData = null!;
    private byte[] _largePcapData = null!;
    private FormatDefinition _pcapFormat = null!;

    [GlobalSetup]
    public void Setup()
    {
        var formatsDir = FindFormatsDirectory();

        var pngPath = Path.Combine(formatsDir, "png.bdef.yaml");
        if (File.Exists(pngPath))
        {
            _pngFormat = new YamlFormatLoader().Load(pngPath);
            _miniPngData = CreateScaledPng(1, 12);
            _mediumPngData = CreateScaledPng(100, 100);
            _largePngData = CreateScaledPng(10_000, 100);
        }

        var pcapPath = Path.Combine(formatsDir, "pcap.bdef.yaml");
        if (File.Exists(pcapPath))
        {
            _pcapFormat = new YamlFormatLoader().Load(pcapPath);
            _mediumPcapData = CreateScaledPcap(100);
            _largePcapData = CreateScaledPcap(10_000);
        }

        // Simple format for fast benchmarking even without format files
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

    [Benchmark]
    public object? DecodeMediumPng()
    {
        if (_pngFormat is null) return null;
        return new BinaryDecoder().Decode(_mediumPngData, _pngFormat);
    }

    [Benchmark]
    public object? DecodeLargePng()
    {
        if (_pngFormat is null) return null;
        return new BinaryDecoder().Decode(_largePngData, _pngFormat);
    }

    [Benchmark]
    public object? DecodeMediumPcap()
    {
        if (_pcapFormat is null) return null;
        return new BinaryDecoder().Decode(_mediumPcapData, _pcapFormat);
    }

    [Benchmark]
    public object? DecodeLargePcap()
    {
        if (_pcapFormat is null) return null;
        return new BinaryDecoder().Decode(_largePcapData, _pcapFormat);
    }

    /// <summary>
    /// Creates a scaled PNG binary with configurable IDAT chunk count and data size.
    /// Signature(8) + IHDR(25) + IDAT*N(12+dataSize each) + IEND(12)
    /// </summary>
    private static byte[] CreateScaledPng(int idatCount, int idatDataSize)
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

        // IDAT chunks
        var idatData = new byte[idatDataSize];
        for (var i = 0; i < idatCount; i++)
        {
            WriteChunk(ms, "IDAT"u8, idatData);
        }

        // IEND chunk
        WriteChunk(ms, "IEND"u8, []);

        return ms.ToArray();
    }

    /// <summary>
    /// Creates a scaled PCAP binary with configurable packet count.
    /// Each packet: Ethernet(14) + IPv4(20) + TCP(20) = 54 bytes payload.
    /// Global header(24) + N * (packet header(16) + payload(54))
    /// </summary>
    private static byte[] CreateScaledPcap(int packetCount)
    {
        const int payloadSize = 54; // Ethernet + IPv4 + TCP minimum headers
        var totalSize = 24 + packetCount * (16 + payloadSize);
        var data = new byte[totalSize];
        var span = data.AsSpan();
        var offset = 0;

        // PCAP global header (little-endian)
        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], 0xA1B2C3D4); offset += 4; // magic
        BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], 2);          offset += 2; // version_major
        BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], 4);          offset += 2; // version_minor
        BinaryPrimitives.WriteInt32LittleEndian(span[offset..], 0);           offset += 4; // thiszone
        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], 0);          offset += 4; // sigfigs
        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], 65535);      offset += 4; // snaplen
        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], 1);          offset += 4; // network (ETHERNET)

        for (var i = 0; i < packetCount; i++)
        {
            // Packet record header (little-endian)
            BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], (uint)i);         offset += 4; // ts_sec
            BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], 0);               offset += 4; // ts_usec
            BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], payloadSize);     offset += 4; // incl_len
            BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], payloadSize);     offset += 4; // orig_len

            var payloadStart = offset;

            // Ethernet frame (big-endian fields)
            // dst_mac (6 bytes)
            span[offset..][..6].Fill(0xFF); offset += 6;
            // src_mac (6 bytes)
            span[offset..][..6].Fill(0x00); offset += 6;
            // ether_type = 0x0800 (IPv4)
            BinaryPrimitives.WriteUInt16BigEndian(span[offset..], 0x0800); offset += 2;

            // IPv4 header (big-endian, 20 bytes minimum)
            span[offset] = 0x45; offset++;            // version=4, IHL=5
            span[offset] = 0x00; offset++;            // TOS
            BinaryPrimitives.WriteUInt16BigEndian(span[offset..], 40); offset += 2; // total_length (20 IPv4 + 20 TCP)
            BinaryPrimitives.WriteUInt16BigEndian(span[offset..], (ushort)i); offset += 2; // identification
            BinaryPrimitives.WriteUInt16BigEndian(span[offset..], 0x4000); offset += 2; // flags=DF, fragment_offset=0
            span[offset] = 64; offset++;              // TTL
            span[offset] = 6; offset++;               // protocol = TCP
            BinaryPrimitives.WriteUInt16BigEndian(span[offset..], 0); offset += 2; // header_checksum
            // src_ip = 192.168.1.1
            span[offset++] = 192; span[offset++] = 168; span[offset++] = 1; span[offset++] = 1;
            // dst_ip = 192.168.1.2
            span[offset++] = 192; span[offset++] = 168; span[offset++] = 1; span[offset++] = 2;

            // TCP segment (big-endian, 20 bytes minimum)
            BinaryPrimitives.WriteUInt16BigEndian(span[offset..], 12345); offset += 2;   // src_port
            BinaryPrimitives.WriteUInt16BigEndian(span[offset..], 80); offset += 2;      // dst_port
            BinaryPrimitives.WriteUInt32BigEndian(span[offset..], (uint)i * 100); offset += 4; // seq_number
            BinaryPrimitives.WriteUInt32BigEndian(span[offset..], 0); offset += 4;       // ack_number
            BinaryPrimitives.WriteUInt16BigEndian(span[offset..], 0x5002); offset += 2;  // data_offset=5, SYN flag
            BinaryPrimitives.WriteUInt16BigEndian(span[offset..], 65535); offset += 2;   // window_size
            BinaryPrimitives.WriteUInt16BigEndian(span[offset..], 0); offset += 2;       // checksum
            BinaryPrimitives.WriteUInt16BigEndian(span[offset..], 0); offset += 2;       // urgent_pointer

            // No TCP payload (size: remaining = 0)
            System.Diagnostics.Debug.Assert(offset - payloadStart == payloadSize);
        }

        return data;
    }

    internal static string FindFormatsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "formats");
            if (Directory.Exists(candidate) && Directory.GetFiles(candidate, "*.bdef.yaml").Length > 0)
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not find 'formats' directory with .bdef.yaml files.");
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
