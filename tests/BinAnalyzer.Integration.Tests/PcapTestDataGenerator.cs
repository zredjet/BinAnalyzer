using System.Buffers.Binary;
using System.Text;

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

    // ===== REQ-188: 複数のプロトコル・バイト順・pcapng =====
    private static byte[] Be(params int[] u16) => u16.SelectMany(v => new[] { (byte)(v >> 8), (byte)v }).ToArray();
    private static byte[] Cat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

    private static byte[] Ipv4(int protocol, byte[] payload, byte[] src, byte[] dst)
    {
        var header = Cat([0x45, 0x00], Be(20 + payload.Length, 0x1234, 0x4000), [64, (byte)protocol, 0, 0], src, dst);
        var sum = 0;
        for (var i = 0; i < 20; i += 2) sum += (header[i] << 8) | header[i + 1];
        while (sum > 0xFFFF) sum = (sum & 0xFFFF) + (sum >> 16);
        header[10] = (byte)(~sum >> 8); header[11] = (byte)~sum;
        return Cat(header, payload);
    }

    private static byte[] Udp(int src, int dst, byte[] data) => Cat(Be(src, dst, 8 + data.Length, 0), data);

    private static byte[] Ethernet(int etherType, byte[] payload)
    {
        var frame = Cat([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x11, 0x22, 0x33, 0x44, 0x55], Be(etherType), payload);
        return frame.Length >= 60 ? frame : Cat(frame, new byte[60 - frame.Length]);
    }

    private static readonly byte[] Client = [192, 168, 1, 10];
    private static readonly byte[] Server = [8, 8, 8, 8];

    /// <summary>DNS の問い合わせ（www.example.com A）。</summary>
    private static readonly byte[] DnsQuery = Cat(Be(0x1234, 0x0100, 1, 0, 0, 0),
        [3, (byte)'w', (byte)'w', (byte)'w', 7, (byte)'e', (byte)'x', (byte)'a', (byte)'m', (byte)'p', (byte)'l', (byte)'e', 3, (byte)'c', (byte)'o', (byte)'m', 0], Be(1, 1));

    /// <summary>Ethernet のフレームの見本: UDP の DNS・ARP の要求・VLAN 100 の ICMP エコー・IPv6 の UDP。</summary>
    private static byte[][] SampleFrames()
    {
        var icmp = Cat([8, 0, 0, 0], Be(7, 1), "ping"u8.ToArray());
        var arp = Cat(Be(1, 0x0800), [6, 4], Be(1), [0x00, 0x11, 0x22, 0x33, 0x44, 0x55], Client, new byte[6], [192, 168, 1, 1]);
        var v6src = Convert.FromHexString("20010DB8000000000000000000000001");
        var v6dst = Convert.FromHexString("20010DB8000000000000000000000002");
        var udp6 = Udp(5353, 53, DnsQuery);
        var ipv6 = Cat([0x60, 0, 0, 0], Be(udp6.Length), [17, 64], v6src, v6dst, udp6);
        return
        [
            Ethernet(0x0800, Ipv4(17, Udp(53000, 53, DnsQuery), Client, Server)),
            Ethernet(0x0806, arp),
            Ethernet(0x8100, Cat(Be((3 << 13) | 100, 0x0800), Ipv4(1, icmp, Client, Server))),
            Ethernet(0x86DD, ipv6),
        ];
    }

    private static byte[] PcapFile(byte[][] packets, bool bigEndian, bool nanosecond, uint linkType)
    {
        var ms = new MemoryStream();
        void U32(uint v) { var b = BitConverter.GetBytes(v); if (bigEndian == BitConverter.IsLittleEndian) Array.Reverse(b); ms.Write(b); }
        void U16(ushort v) { var b = BitConverter.GetBytes(v); if (bigEndian == BitConverter.IsLittleEndian) Array.Reverse(b); ms.Write(b); }
        U32(nanosecond ? 0xA1B23C4Du : 0xA1B2C3D4u); U16(2); U16(4); U32(0); U32(0); U32(65535); U32(linkType);
        for (var i = 0; i < packets.Length; i++)
        {
            U32((uint)(1758700000 + i)); U32(nanosecond ? 123456789u : 123456u); U32((uint)packets[i].Length); U32((uint)packets[i].Length);
            ms.Write(packets[i]);
        }
        return ms.ToArray();
    }

    /// <summary>リトルエンディアン・マイクロ秒の pcap（Ethernet）: UDP の DNS・ARP・VLAN の ICMP・IPv6 の UDP。tcpdump -r で読めることを確かめた。</summary>
    public static byte[] CreateEthernetMixPcap() => PcapFile(SampleFrames(), bigEndian: false, nanosecond: false, linkType: 1);

    /// <summary>ビッグエンディアン・ナノ秒の pcap（マジック 0xA1B23C4D を BE で書く）。</summary>
    public static byte[] CreateBigEndianNanosecondPcap() => PcapFile(SampleFrames()[..2], bigEndian: true, nanosecond: true, linkType: 1);

    /// <summary>リンク層の種類が RAW（101）の pcap: IPv4 の ICMP と IPv6 の UDP（先頭 4 ビットの版で分ける）。</summary>
    public static byte[] CreateRawIpPcap()
    {
        var frames = SampleFrames();
        return PcapFile([frames[2][18..(18 + 32)], frames[3][14..]], bigEndian: false, nanosecond: false, linkType: 101);
    }

    /// <summary>
    /// pcapng（リトルエンディアン）: SHB（shb_userappl）・IDB（Ethernet、if_name・if_tsresol）・EPB × 2（1 つ目に opt_comment）・SPB・NRB（8.8.8.8 = dns.google）・ISB。
    /// </summary>
    public static byte[] CreatePcapNg()
    {
        static byte[] Pad(byte[] b) => Cat(b, new byte[(4 - b.Length % 4) % 4]);
        static byte[] Opt(ushort code, byte[] value) => Cat(BitConverter.GetBytes(code), BitConverter.GetBytes((ushort)value.Length), Pad(value));
        static byte[] EndOpt() => Opt(0, []);
        static byte[] Block(uint type, byte[] body)
        {
            body = Pad(body);
            var length = (uint)(12 + body.Length);
            return Cat(BitConverter.GetBytes(type), BitConverter.GetBytes(length), body, BitConverter.GetBytes(length));
        }
        var frames = SampleFrames();
        var shb = Block(0x0A0D0D0A, Cat(BitConverter.GetBytes(0x1A2B3C4Du), BitConverter.GetBytes((ushort)1), BitConverter.GetBytes((ushort)0),
            BitConverter.GetBytes(-1L), Opt(4, "binanalyzer-test"u8.ToArray()), EndOpt()));
        var idb = Block(1, Cat(BitConverter.GetBytes((ushort)1), BitConverter.GetBytes((ushort)0), BitConverter.GetBytes(65535u),
            Opt(2, "en0"u8.ToArray()), Opt(9, [6]), EndOpt()));
        byte[] Epb(int i, byte[] frame, byte[] options) => Block(6, Cat(BitConverter.GetBytes(0u), BitConverter.GetBytes(0x00063F00u),
            BitConverter.GetBytes((uint)(i * 1000)), BitConverter.GetBytes((uint)frame.Length), BitConverter.GetBytes((uint)frame.Length), Pad(frame), options));
        var epb0 = Epb(0, frames[0], Cat(Opt(1, Encoding.UTF8.GetBytes("コメント")), EndOpt()));
        var epb1 = Epb(1, frames[2], []);
        var spb = Block(3, Cat(BitConverter.GetBytes((uint)frames[1].Length), frames[1]));
        var nrb = Block(4, Cat(BitConverter.GetBytes((ushort)1), BitConverter.GetBytes((ushort)16), Server, "dns.google\0\0"u8.ToArray(),
            BitConverter.GetBytes((ushort)0), BitConverter.GetBytes((ushort)0)));
        var isb = Block(5, Cat(BitConverter.GetBytes(0u), BitConverter.GetBytes(0x00063F00u), BitConverter.GetBytes(5000u),
            Opt(4, BitConverter.GetBytes(4UL)), EndOpt()));
        return Cat(shb, idb, epb0, epb1, spb, nrb, isb);
    }

    /// <summary>
    /// UDP の DNS を含む pcap（REQ-198）: DNS の問い合わせ（53 番宛て）、DNS の応答（53 番から。DnsTestDataGenerator.CreateDnsResponse の圧縮ポインタ付きのメッセージ）、
    /// mDNS の応答（5353 番、printer.local の A、クラスの cache flush のビット付き）、DNS でない UDP（1234 番）。tcpdump -r で同じ内容に読めることを確かめた。
    /// </summary>
    public static byte[] CreateDnsPcap()
    {
        var mdns = Cat(Be(0, 0x8400, 0, 1, 0, 0),
            [7, (byte)'p', (byte)'r', (byte)'i', (byte)'n', (byte)'t', (byte)'e', (byte)'r', 5, (byte)'l', (byte)'o', (byte)'c', (byte)'a', (byte)'l', 0],
            Be(1, 0x8001), Be(0, 120), Be(4), [192, 168, 1, 50]);
        byte[] mdnsGroup = [224, 0, 0, 251];
        byte[] printer = [192, 168, 1, 50];
        return PcapFile(
        [
            Ethernet(0x0800, Ipv4(17, Udp(53000, 53, DnsQuery), Client, Server)),
            Ethernet(0x0800, Ipv4(17, Udp(53, 53000, DnsTestDataGenerator.CreateDnsResponse()), Server, Client)),
            Ethernet(0x0800, Ipv4(17, Udp(5353, 5353, mdns), printer, mdnsGroup)),
            Ethernet(0x0800, Ipv4(17, Udp(40000, 1234, "not dns"u8.ToArray()), Client, Server)),
        ], bigEndian: false, nanosecond: false, linkType: 1);
    }
}
