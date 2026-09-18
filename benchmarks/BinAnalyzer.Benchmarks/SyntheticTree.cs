using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Benchmarks;

/// <summary>
/// PCAP 風の合成デコード結果ツリー。パケットごとに
/// packet(4 int) → ethernet(2 bytes + 1 int) → ipv4(11 int / bytes + 2 virtual) → tcp(8 int + 1 bitfield + payload bytes) の
/// 約 38 ノードを持つ。エンジンを通さずに任意のノード数のツリーを作るためのもの。
/// </summary>
public static class SyntheticTree
{
    public const int NodesPerPacket = 38;

    public static (DecodedStruct Root, byte[] Data) Build(int nodeCount, int payloadSize = 0)
    {
        var packetCount = Math.Max(1, nodeCount / NodesPerPacket);
        var packetSize = 16 + 14 + 20 + 20 + payloadSize;
        var data = new byte[24 + (long)packetCount * packetSize];
        var mem = data.AsMemory();

        var header = new DecodedStruct
        {
            Name = "header", StructType = "pcap_header", Offset = 0, Size = 24,
            Children =
            [
                new DecodedInteger { Name = "magic", Offset = 0, Size = 4, Value = 0xA1B2C3D4, ValidationPassed = true },
                new DecodedInteger { Name = "version_major", Offset = 4, Size = 2, Value = 2 },
                new DecodedInteger { Name = "version_minor", Offset = 6, Size = 2, Value = 4 },
                new DecodedInteger { Name = "thiszone", Offset = 8, Size = 4, Value = 0 },
                new DecodedInteger { Name = "sigfigs", Offset = 12, Size = 4, Value = 0 },
                new DecodedInteger { Name = "snaplen", Offset = 16, Size = 4, Value = 65535 },
                new DecodedInteger { Name = "network", Offset = 20, Size = 4, Value = 1, EnumLabel = "ETHERNET" },
            ],
        };

        var packets = new DecodedNode[packetCount];
        long off = 24;
        for (var i = 0; i < packetCount; i++)
        {
            packets[i] = Packet(i, off, payloadSize, mem);
            off += packetSize;
        }

        var root = new DecodedStruct
        {
            Name = "PCAP", StructType = "pcap", Offset = 0, Size = data.Length,
            Children =
            [
                header,
                new DecodedArray { Name = "packets", Offset = 24, Size = data.Length - 24, Elements = packets },
            ],
        };
        return (root, data);
    }

    private static DecodedStruct Packet(int i, long o, int payloadSize, ReadOnlyMemory<byte> mem)
    {
        var tcp = new DecodedStruct
        {
            Name = "body", StructType = "tcp_segment", Offset = o + 50, Size = 20 + payloadSize,
            Children =
            [
                new DecodedInteger { Name = "src_port", Offset = o + 50, Size = 2, Value = 12345 },
                new DecodedInteger { Name = "dst_port", Offset = o + 52, Size = 2, Value = 80 },
                new DecodedInteger { Name = "seq_number", Offset = o + 54, Size = 4, Value = i * 100L },
                new DecodedInteger { Name = "ack_number", Offset = o + 58, Size = 4, Value = 0 },
                new DecodedBitfield { Name = "data_offset_flags", Offset = o + 62, Size = 2, RawValue = 0x5002, Fields = [] },
                new DecodedInteger { Name = "window_size", Offset = o + 64, Size = 2, Value = 65535 },
                new DecodedInteger { Name = "checksum", Offset = o + 66, Size = 2, Value = 0, ChecksumAlgorithm = "tcp", ChecksumValid = (i & 7) != 0 },
                new DecodedInteger { Name = "urgent_pointer", Offset = o + 68, Size = 2, Value = 0 },
                new DecodedBytes { Name = "payload", Offset = o + 70, Size = payloadSize, RawBytes = mem.Slice((int)(o + 70), payloadSize) },
            ],
        };
        var ipv4 = new DecodedStruct
        {
            Name = "payload", StructType = "ipv4_packet", Offset = o + 30, Size = 40 + payloadSize,
            Children =
            [
                new DecodedInteger { Name = "version_ihl", Offset = o + 30, Size = 1, Value = 0x45 },
                new DecodedVirtual { Name = "version", Offset = o + 30, Size = 0, Value = 4L },
                new DecodedVirtual { Name = "ihl", Offset = o + 30, Size = 0, Value = 5L },
                new DecodedInteger { Name = "tos", Offset = o + 31, Size = 1, Value = 0 },
                new DecodedInteger { Name = "total_length", Offset = o + 32, Size = 2, Value = 40 + payloadSize },
                new DecodedInteger { Name = "identification", Offset = o + 34, Size = 2, Value = i & 0xFFFF },
                new DecodedInteger { Name = "flags_fragment", Offset = o + 36, Size = 2, Value = 0x4000 },
                new DecodedInteger { Name = "ttl", Offset = o + 38, Size = 1, Value = 64 },
                new DecodedInteger { Name = "protocol", Offset = o + 39, Size = 1, Value = 6, EnumLabel = "TCP" },
                new DecodedInteger { Name = "header_checksum", Offset = o + 40, Size = 2, Value = 0 },
                new DecodedBytes { Name = "src_ip", Offset = o + 42, Size = 4, RawBytes = mem.Slice((int)(o + 42), 4) },
                new DecodedBytes { Name = "dst_ip", Offset = o + 46, Size = 4, RawBytes = mem.Slice((int)(o + 46), 4) },
                tcp,
            ],
        };
        var ethernet = new DecodedStruct
        {
            Name = "data", StructType = "ethernet_frame", Offset = o + 16, Size = 54 + payloadSize,
            Children =
            [
                new DecodedBytes { Name = "dst_mac", Offset = o + 16, Size = 6, RawBytes = mem.Slice((int)(o + 16), 6) },
                new DecodedBytes { Name = "src_mac", Offset = o + 22, Size = 6, RawBytes = mem.Slice((int)(o + 22), 6) },
                new DecodedInteger { Name = "ether_type", Offset = o + 28, Size = 2, Value = 0x0800, EnumLabel = "IPv4" },
                ipv4,
            ],
        };
        return new DecodedStruct
        {
            Name = "packets", StructType = "pcap_packet", Offset = o, Size = 70 + payloadSize,
            Children =
            [
                new DecodedInteger { Name = "ts_sec", Offset = o, Size = 4, Value = i },
                new DecodedInteger { Name = "ts_usec", Offset = o + 4, Size = 4, Value = 0 },
                new DecodedInteger { Name = "incl_len", Offset = o + 8, Size = 4, Value = 54 + payloadSize },
                new DecodedInteger { Name = "orig_len", Offset = o + 12, Size = 4, Value = 54 + payloadSize },
                ethernet,
            ],
        };
    }
}
