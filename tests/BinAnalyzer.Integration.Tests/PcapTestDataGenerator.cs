using System.Buffers.Binary;

namespace BinAnalyzer.Integration.Tests;

public static class PcapTestDataGenerator
{
    /// <summary>
    /// 最小PCAPファイル: pcap_header(24B) + 1 packet(16B packet header + 54B ethernet+IPv4+TCP) = 94バイト
    /// リトルエンディアン、Ethernet link type、1パケット（IPv4/TCP）
    /// </summary>
    public static byte[] CreateMinimalPcap()
    {
        var data = new byte[94];
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

        // === pcap_packet (16B header + 54B data = 70 bytes) ===
        // ts_sec: 1000000
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1000000); pos += 4;

        // ts_usec: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;

        // incl_len: 54 (ethernet 14 + IPv4 20 + TCP 20)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 54); pos += 4;

        // orig_len: 54
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 54); pos += 4;

        // === ethernet_frame (big-endian, 14 bytes) ===
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

        // total_length: 40 (IPv4 20 + TCP 20)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 40); pos += 2;

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
        pos += 4;

        // === tcp_segment (big-endian, 20 bytes) ===
        // src_port: 80
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 80); pos += 2;

        // dst_port: 12345
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 12345); pos += 2;

        // seq_number: 1
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4;

        // ack_number: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // data_offset_flags: 0x5002 (data_offset=5, SYN=1)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x5002); pos += 2;

        // window_size: 65535
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 65535); pos += 2;

        // checksum: 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // urgent_pointer: 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0);

        return data;
    }

    /// <summary>
    /// IPv6パケット付きPCAP: pcap_header(24B) + 1 packet(16B + 14B ethernet + 40B IPv6 + 8B UDP) = 102バイト
    /// </summary>
    public static byte[] CreatePcapWithIpv6()
    {
        var data = new byte[102];
        var span = data.AsSpan();
        var pos = 0;

        // === pcap_header (24 bytes, little-endian) ===
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0xA1B2C3D4); pos += 4;
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 2); pos += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 4); pos += 2;
        BinaryPrimitives.WriteInt32LittleEndian(span[pos..], 0); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 65535); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1); pos += 4; // ETHERNET

        // === pcap_packet header (16 bytes) ===
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1000000); pos += 4; // ts_sec
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4; // ts_usec
        // incl_len: 62 (ethernet 14 + IPv6 40 + UDP 8)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 62); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 62); pos += 4; // orig_len

        // === ethernet_frame (big-endian) ===
        // dst_mac
        data[pos] = 0x33; data[pos + 1] = 0x33; data[pos + 2] = 0x00;
        data[pos + 3] = 0x00; data[pos + 4] = 0x00; data[pos + 5] = 0x01;
        pos += 6;
        // src_mac
        data[pos] = 0x00; data[pos + 1] = 0x11; data[pos + 2] = 0x22;
        data[pos + 3] = 0x33; data[pos + 4] = 0x44; data[pos + 5] = 0x55;
        pos += 6;
        // ether_type: 0x86DD (IPv6)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x86DD); pos += 2;

        // === ipv6_packet (big-endian, 40 bytes header + 8 bytes UDP) ===
        // version_tc_flow: version=6, tc=0, flow=0 → 0x60000000
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x60000000); pos += 4;

        // payload_length: 8 (UDP only)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 8); pos += 2;

        // next_header: 17 (UDP)
        data[pos] = 17; pos += 1;

        // hop_limit: 64
        data[pos] = 64; pos += 1;

        // src_ip: ::1 (16 bytes)
        data[pos + 15] = 1;
        pos += 16;

        // dst_ip: ::1 (16 bytes)
        data[pos + 15] = 1;
        pos += 16;

        // === udp_datagram (big-endian, 8 bytes) ===
        // src_port: 5353
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 5353); pos += 2;

        // dst_port: 5353
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 5353); pos += 2;

        // length: 8
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 8); pos += 2;

        // checksum: 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0);

        return data;
    }
}
