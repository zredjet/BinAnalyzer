using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class PcapParsingTests
{
    private static readonly string PcapFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "pcap.bdef.yaml");

    [Fact]
    public void PcapFormat_LoadsWithoutErrors()
    {
        var result = FormatValidator.Validate(new YamlFormatLoader().Load(PcapFormatPath));
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void PcapFormat_Header_DecodesCorrectly()
    {
        var file = Decode(PcapTestDataGenerator.CreateMinimalPcap()).Child("body");

        ((DecodedStruct)file).StructType.Should().Be("pcap_file");
        file.Child("magic").Validation!.Passed.Should().BeTrue();
        file.Child("nanosecond").Int().Should().Be(0);
        file.Child("version_major").Int().Should().Be(2);
        file.Child("network").Label().Should().Be("ETHERNET");
    }

    [Fact]
    public void PcapFormat_TcpSegment_DecodesCorrectly()
    {
        var ip = Packets(PcapTestDataGenerator.CreateMinimalPcap())[0].Child("data").Child("payload").Child("packet");

        ip.Child("src_ip").Str().Should().Be("192.168.1.1");
        ip.Child("dst_ip").Str().Should().Be("192.168.1.2");
        ip.Child("protocol").Label().Should().Be("TCP");
        var tcp = ip.Child("body");
        tcp.Child("src_port").Int().Should().Be(80);
        tcp.Child("dst_port").Int().Should().Be(12345);
        tcp.Child("data_offset_flags").Bits("syn").Should().Be(1);
    }

    [Fact]
    public void PcapFormat_Ipv4Options_AreSkippedByIhl()
    {
        var ip = Packets(PcapTestDataGenerator.CreatePcapWithIpv4Options())[0].Child("data").Child("payload").Child("packet");

        ip.Child("ihl").Int().Should().Be(6);
        ip.Child("options").Size.Should().Be(4);
        ((DecodedStruct)ip.Child("body")).StructType.Should().Be("tcp_segment");
    }

    [Fact]
    public void PcapFormat_TcpOptions_DecodeKinds()
    {
        var tcp = Packets(PcapTestDataGenerator.CreatePcapWithTcpOptions())[0].Find("body");

        tcp.Child("data_offset_flags").Bits("data_offset").Should().Be(8);
        var options = tcp.Child("options").Elements();
        options.Should().NotBeEmpty();
        options.Select(o => o.Child("kind").Label()).Should().Contain("MSS");
    }

    [Fact]
    public void PcapFormat_Ipv6_DecodesAddressesAndUdp()
    {
        var ip = Packets(PcapTestDataGenerator.CreatePcapWithIpv6())[0].Child("data").Child("payload").Child("packet");

        ((DecodedStruct)ip).StructType.Should().Be("ipv6_packet");
        ip.Child("next_header").Label().Should().Be("UDP");
        ip.Child("src_ip").Str().Should().Contain(":");
        ((DecodedStruct)ip.Child("payload").Child("upper_layer")).StructType.Should().Be("udp_datagram");
    }

    [Fact]
    public void PcapFormat_EthernetMix_DecodesUdpArpVlanAndIpv6()
    {
        var packets = Packets(PcapTestDataGenerator.CreateEthernetMixPcap());

        packets.Should().HaveCount(4);
        var dns = packets[0].Child("data");
        dns.Child("src_mac").Str().Should().Be("00:11:22:33:44:55");
        dns.Child("dst_mac").Str().Should().Be("ff:ff:ff:ff:ff:ff");
        var udp = dns.Child("payload").Child("packet").Child("body");
        udp.Child("dst_port").Int().Should().Be(53);
        udp.Child("payload").Size.Should().Be(33);

        var arp = packets[1].Child("data").Child("payload");
        arp.Child("operation").Label().Should().Be("request");
        arp.Child("trailer").Size.Should().Be(18);                      // 60 バイトへの詰め物

        var vlan = packets[2].Child("data");
        vlan.Child("vlan_tags").Elements().Single().Child("tci").Bits("vlan_id").Should().Be(100);
        var vlanIp = vlan.Child("payload").Child("packet");
        var icmp = vlanIp.Child("body");
        icmp.Child("icmp_type").Int().Should().Be(8);
        icmp.Child("identifier").Int().Should().Be(7);
        // IPv4 の total_length で区切り、Ethernet の最小長（60 バイト）への詰め物は中身に含めない（旧定義は TCP / UDP の中身に含めていた）
        icmp.Size.Should().Be(12);
        vlanIp.Child("link_padding").Size.Should().Be(10);

        var v6 = packets[3].Child("data").Child("payload").Child("packet");
        v6.Child("src_ip").Str().Should().Be("2001:db8:0:0:0:0:0:1");
        v6.Child("payload").Child("upper_layer").Child("dst_port").Int().Should().Be(53);
    }

    [Fact]
    public void PcapFormat_BigEndianNanosecond_UsesMagicForByteOrder()
    {
        var file = Decode(PcapTestDataGenerator.CreateBigEndianNanosecondPcap()).Child("body");

        file.Child("magic").Int().Should().Be(0xA1B23C4D);
        file.Child("nanosecond").Int().Should().Be(1);
        file.Child("snaplen").Int().Should().Be(65535);
        var packets = file.Child("packets").Elements();
        packets[0].Child("ts_frac").Int().Should().Be(123456789);
        packets[0].Child("data").Child("payload").Child("packet").Child("src_ip").Str().Should().Be("192.168.1.10");
    }

    [Fact]
    public void PcapFormat_RawIp_DispatchesByVersionNibble()
    {
        var packets = Packets(PcapTestDataGenerator.CreateRawIpPcap());

        ((DecodedStruct)packets[0].Child("data").Child("packet")).StructType.Should().Be("ipv4_packet");
        ((DecodedStruct)packets[1].Child("data").Child("packet")).StructType.Should().Be("ipv6_packet");
    }

    [Fact]
    public void PcapFormat_PcapNg_DecodesBlocksAndPackets()
    {
        var file = Decode(PcapTestDataGenerator.CreatePcapNg()).Child("body");

        ((DecodedStruct)file).StructType.Should().Be("pcapng_file");
        var blocks = file.Child("blocks").Elements();
        blocks.Select(b => b.Child("block_type").Label()).Should().Equal("SHB", "IDB", "EPB", "EPB", "SPB", "NRB", "ISB");
        blocks.Should().OnlyContain(b => b.Child("block_total_length_trailer").Validation!.Passed);
        blocks[0].Child("block_body").Child("byte_order_magic").Validation!.Passed.Should().BeTrue();
        blocks[1].Child("block_body").Child("link_type").Label().Should().Be("ETHERNET");

        var epb = blocks[2].Child("block_body");
        epb.Child("packet_data").Child("payload").Child("packet").Child("dst_ip").Str().Should().Be("8.8.8.8");
        epb.Child("packet_data").Child("payload").Child("packet").Child("body").Child("payload")
            .Child("questions").Elements().Single().Child("qname").Child("full_name").Str().Should().Be("www.example.com.");   // UDP 53 番は DNS として読む
        epb.Child("options").Elements()[0].Child("comment").Str().Should().Be("コメント");
        blocks[4].Child("block_body").Child("packet_data").Child("payload").Child("operation").Label().Should().Be("request");
        var record = blocks[5].Child("block_body").Child("records").Elements()[0];
        record.Child("ipv4_address").Str().Should().Be("8.8.8.8");
    }

    [Fact]
    public void PcapFormat_UdpDns_IsDecodedWithTheDnsDefinition()
    {
        var packets = Packets(PcapTestDataGenerator.CreateDnsPcap());

        var query = UdpOf(packets[0]).Child("payload");
        ((DecodedStruct)query).StructType.Should().Be("dns");
        query.Child("questions").Elements().Single().Child("qname").Child("full_name").Str().Should().Be("www.example.com.");

        // 圧縮ポインタはメッセージ（UDP のペイロード）の先頭からの位置として読む
        var response = UdpOf(packets[1]).Child("payload");
        var answers = response.Child("answers").Elements();
        answers.Select(a => a.Child("name").Child("full_name").Str()).Should().Equal(
            "www.example.com.", "www.example.com.", "example.com.", "example.com.", "cdn.example.com.", "example.com.");
        answers[2].Child("body").Child("rdata").Child("exchange").Child("full_name").Str().Should().Be("mail.example.com.");
        answers[0].Child("body").Child("rdata").Child("address").Str().Should().Be("93.184.216.34");
    }

    [Fact]
    public void PcapFormat_UdpDns_MatchesTheStandaloneDecode()
    {
        var embedded = UdpOf(Packets(PcapTestDataGenerator.CreateDnsPcap())[1]).Child("payload");
        var standalone = new BinaryDecoder().Decode(DnsTestDataGenerator.CreateDnsResponse(),
            new YamlFormatLoader().Load(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "dns.bdef.yaml")));

        static List<string> Names(DecodedNode node) => node.FindAll("full_name").Select(n => n.Str()).ToList();
        Names(embedded).Should().Equal(Names(standalone));
        embedded.Size.Should().Be(DnsTestDataGenerator.CreateDnsResponse().Length);
    }

    [Fact]
    public void PcapFormat_Mdns_DecodesTheCacheFlushBit()
    {
        var mdns = UdpOf(Packets(PcapTestDataGenerator.CreateDnsPcap())[2]).Child("payload");

        var answer = mdns.Child("answers").Elements().Single();
        answer.Child("name").Child("full_name").Str().Should().Be("printer.local.");
        var classField = (DecodedBitfield)answer.Child("body").Child("class_field");
        classField.Fields.Should().Contain(f => f.Name == "cache_flush" && f.Value == 1);
        classField.Fields.Should().Contain(f => f.Name == "rr_class" && f.EnumLabel == "IN");
    }

    [Fact]
    public void PcapFormat_NonDnsUdp_KeepsThePayloadAsBytes()
    {
        var payload = UdpOf(Packets(PcapTestDataGenerator.CreateDnsPcap())[3]).Child("payload");

        payload.Should().BeOfType<DecodedBytes>().Which.RawBytes.ToArray().Should().Equal("not dns"u8.ToArray());
    }

    [Fact]
    public void PcapFormat_TreeOutput_ContainsExpectedElements()
    {
        var output = new TreeOutputFormatter().Format(Decode(PcapTestDataGenerator.CreateEthernetMixPcap()));

        output.Should().Contain("PCAP");
        output.Should().Contain("192.168.1.10");
        output.Should().NotContain("magic_be");
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(PcapFormatPath));

    private static IReadOnlyList<DecodedNode> Packets(byte[] data) => Decode(data).Child("body").Child("packets").Elements();

    private static DecodedNode UdpOf(DecodedNode packet) => packet.Child("data").Child("payload").Child("packet").Child("body");
}
