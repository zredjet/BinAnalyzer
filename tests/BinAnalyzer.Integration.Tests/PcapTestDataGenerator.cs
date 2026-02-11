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

    /// <summary>
    /// IPv4オプション付きPCAP: IHL=6（4バイトオプション）+ TCP data_offset=5
    /// pcap_header(24B) + packet_header(16B) + ethernet(14B) + IPv4(24B) + TCP(20B) = 98バイト
    /// </summary>
    public static byte[] CreatePcapWithIpv4Options()
    {
        var data = new byte[98];
        var span = data.AsSpan();
        var pos = 0;

        // === pcap_header (24 bytes) ===
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0xA1B2C3D4); pos += 4;
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 2); pos += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 4); pos += 2;
        BinaryPrimitives.WriteInt32LittleEndian(span[pos..], 0); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 65535); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1); pos += 4; // ETHERNET

        // === pcap_packet header (16 bytes) ===
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1000000); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        // incl_len: 58 (ethernet 14 + IPv4 24 + TCP 20)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 58); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 58); pos += 4;

        // === ethernet_frame (14 bytes) ===
        data[pos] = 0xFF; data[pos + 1] = 0xFF; data[pos + 2] = 0xFF;
        data[pos + 3] = 0xFF; data[pos + 4] = 0xFF; data[pos + 5] = 0xFF;
        pos += 6;
        data[pos] = 0x00; data[pos + 1] = 0x11; data[pos + 2] = 0x22;
        data[pos + 3] = 0x33; data[pos + 4] = 0x44; data[pos + 5] = 0x55;
        pos += 6;
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x0800); pos += 2;

        // === ipv4_packet (24 bytes, IHL=6) ===
        data[pos] = 0x46; pos += 1; // version=4, IHL=6
        data[pos] = 0x00; pos += 1; // tos
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 44); pos += 2; // total_length: 24+20=44
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2; // identification
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x4000); pos += 2; // flags_fragment (DF)
        data[pos] = 64; pos += 1; // ttl
        data[pos] = 6; pos += 1; // protocol: TCP
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2; // checksum
        data[pos] = 10; data[pos + 1] = 0; data[pos + 2] = 0; data[pos + 3] = 1; pos += 4; // src_ip
        data[pos] = 10; data[pos + 1] = 0; data[pos + 2] = 0; data[pos + 3] = 2; pos += 4; // dst_ip
        // IPv4 options (4 bytes): NOP NOP NOP EOL
        data[pos] = 0x01; data[pos + 1] = 0x01; data[pos + 2] = 0x01; data[pos + 3] = 0x00;
        pos += 4;

        // === tcp_segment (20 bytes, data_offset=5) ===
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 80); pos += 2; // src_port
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 8080); pos += 2; // dst_port
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 100); pos += 4; // seq
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // ack
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x5002); pos += 2; // data_offset=5, SYN
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 65535); pos += 2; // window
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2; // checksum
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); // urgent_pointer

        return data;
    }

    /// <summary>
    /// TCPオプション付きPCAP: IHL=5 + TCP data_offset=8（12バイトオプション）
    /// pcap_header(24B) + packet_header(16B) + ethernet(14B) + IPv4(20B) + TCP(32B) = 106バイト
    /// </summary>
    public static byte[] CreatePcapWithTcpOptions()
    {
        var data = new byte[106];
        var span = data.AsSpan();
        var pos = 0;

        // === pcap_header (24 bytes) ===
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0xA1B2C3D4); pos += 4;
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 2); pos += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 4); pos += 2;
        BinaryPrimitives.WriteInt32LittleEndian(span[pos..], 0); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 65535); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1); pos += 4; // ETHERNET

        // === pcap_packet header (16 bytes) ===
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1000000); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        // incl_len: 66 (ethernet 14 + IPv4 20 + TCP 32)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 66); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 66); pos += 4;

        // === ethernet_frame (14 bytes) ===
        data[pos] = 0xFF; data[pos + 1] = 0xFF; data[pos + 2] = 0xFF;
        data[pos + 3] = 0xFF; data[pos + 4] = 0xFF; data[pos + 5] = 0xFF;
        pos += 6;
        data[pos] = 0x00; data[pos + 1] = 0x11; data[pos + 2] = 0x22;
        data[pos + 3] = 0x33; data[pos + 4] = 0x44; data[pos + 5] = 0x55;
        pos += 6;
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x0800); pos += 2;

        // === ipv4_packet (20 bytes, IHL=5) ===
        data[pos] = 0x45; pos += 1; // version=4, IHL=5
        data[pos] = 0x00; pos += 1;
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 52); pos += 2; // total_length: 20+32=52
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2;
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x4000); pos += 2;
        data[pos] = 64; pos += 1;
        data[pos] = 6; pos += 1; // TCP
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;
        data[pos] = 10; data[pos + 1] = 0; data[pos + 2] = 0; data[pos + 3] = 1; pos += 4;
        data[pos] = 10; data[pos + 1] = 0; data[pos + 2] = 0; data[pos + 3] = 2; pos += 4;

        // === tcp_segment (32 bytes, data_offset=8) ===
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 443); pos += 2; // src_port
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 54321); pos += 2; // dst_port
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 200); pos += 4; // seq
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // ack
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x8002); pos += 2; // data_offset=8, SYN
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 65535); pos += 2; // window
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2; // checksum
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2; // urgent_pointer
        // TCP options (12 bytes): MSS(4) + NOP + NOP + Timestamps(8, but we use 7+NOP)
        // Kind=2(MSS), Length=4, MSS=1460
        data[pos] = 0x02; data[pos + 1] = 0x04;
        BinaryPrimitives.WriteUInt16BigEndian(span[(pos + 2)..], 1460); pos += 4;
        // NOP NOP
        data[pos] = 0x01; data[pos + 1] = 0x01; pos += 2;
        // Kind=8(Timestamps), Length=6 (simplified)
        data[pos] = 0x08; data[pos + 1] = 0x06;
        BinaryPrimitives.WriteUInt32BigEndian(span[(pos + 2)..], 12345);
        pos += 6;

        return data;
    }
}
