using System.Buffers.Binary;

namespace BinAnalyzer.Integration.Tests;

public static class PcapTestDataGenerator
{
    /// <summary>
    /// 最小PCAPファイル: pcap_header(24B) + 1 packet(16B packet header + 34B ethernet+IPv4) = 74バイト
    /// リトルエンディアン、Ethernet link type、1パケット（IPv4/TCP）
    /// </summary>
    public static byte[] CreateMinimalPcap()
    {
        var data = new byte[74];
        var span = data.AsSpan();
        var pos = 0;

        // === pcap_header (24 bytes, little-endian) ===
        // magic: 0xA1B2C3D4 (LE)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0xA1B2C3D4); pos += 4;

        // version_major: 2
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 2); pos += 2;

        // version_minor: 4
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 4); pos += 2;

        // thiszone: 0
        BinaryPrimitives.WriteInt32LittleEndian(span[pos..], 0); pos += 4;

        // sigfigs: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;

        // snaplen: 65535
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 65535); pos += 4;

        // network: 1 (ETHERNET)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1); pos += 4;

        // === pcap_packet (16B header + 34B data = 50 bytes) ===
        // ts_sec: 1000000
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1000000); pos += 4;

        // ts_usec: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;

        // incl_len: 34 (ethernet header 14 + minimal IPv4 header 20)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 34); pos += 4;

        // orig_len: 34
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 34); pos += 4;

        // === ethernet_frame (big-endian, 34 bytes) ===
        // dst_mac: 6 bytes
        data[pos] = 0xFF; data[pos + 1] = 0xFF; data[pos + 2] = 0xFF;
        data[pos + 3] = 0xFF; data[pos + 4] = 0xFF; data[pos + 5] = 0xFF;
        pos += 6;

        // src_mac: 6 bytes
        data[pos] = 0x00; data[pos + 1] = 0x11; data[pos + 2] = 0x22;
        data[pos + 3] = 0x33; data[pos + 4] = 0x44; data[pos + 5] = 0x55;
        pos += 6;

        // ether_type: 0x0800 (IPv4, big-endian)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x0800); pos += 2;

        // === ipv4_packet (big-endian, 20 bytes) ===
        // version_ihl: 0x45 (version=4, IHL=5)
        data[pos] = 0x45; pos += 1;

        // tos: 0
        data[pos] = 0x00; pos += 1;

        // total_length: 20
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 20); pos += 2;

        // identification: 1
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2;

        // flags_fragment: 0x4000 (DF=1)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x4000); pos += 2;

        // ttl: 64
        data[pos] = 64; pos += 1;

        // protocol: 6 (TCP)
        data[pos] = 6; pos += 1;

        // header_checksum: 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // src_ip: 192.168.1.1
        data[pos] = 192; data[pos + 1] = 168; data[pos + 2] = 1; data[pos + 3] = 1;
        pos += 4;

        // dst_ip: 192.168.1.2
        data[pos] = 192; data[pos + 1] = 168; data[pos + 2] = 1; data[pos + 3] = 2;

        return data;
    }
}
