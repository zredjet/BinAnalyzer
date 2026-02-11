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
        var format = new YamlFormatLoader().Load(PcapFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void PcapFormat_DecodesMinimalPcap()
    {
        var data = PcapTestDataGenerator.CreateMinimalPcap();
        var format = new YamlFormatLoader().Load(PcapFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("PCAP");
        decoded.Children.Should().HaveCount(2);
        decoded.Children[0].Name.Should().Be("header");
        decoded.Children[1].Name.Should().Be("packets");
    }

    [Fact]
    public void PcapFormat_Header_DecodesCorrectly()
    {
        var data = PcapTestDataGenerator.CreateMinimalPcap();
        var format = new YamlFormatLoader().Load(PcapFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var header = decoded.Children[0].Should().BeOfType<DecodedStruct>().Subject;

        var magic = header.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        magic.Name.Should().Be("magic");
        magic.Value.Should().Be(0xA1B2C3D4);

        var network = header.Children[6].Should().BeOfType<DecodedInteger>().Subject;
        network.Name.Should().Be("network");
        network.Value.Should().Be(1);
        network.EnumLabel.Should().Be("ETHERNET");
    }

    [Fact]
    public void PcapFormat_Packet_DecodesCorrectly()
    {
        var data = PcapTestDataGenerator.CreateMinimalPcap();
        var format = new YamlFormatLoader().Load(PcapFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var packets = decoded.Children[1].Should().BeOfType<DecodedArray>().Subject;
        packets.Elements.Should().HaveCount(1);

        var packet = packets.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var inclLen = packet.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        inclLen.Name.Should().Be("incl_len");
        inclLen.Value.Should().Be(54);
    }

    [Fact]
    public void PcapFormat_TcpSegment_DecodesCorrectly()
    {
        var data = PcapTestDataGenerator.CreateMinimalPcap();
        var format = new YamlFormatLoader().Load(PcapFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var packets = decoded.Children[1].Should().BeOfType<DecodedArray>().Subject;
        var packet = packets.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        // data → ethernet_frame → payload → ipv4_packet → body → tcp_segment
        var ethFrame = packet.Children[4].Should().BeOfType<DecodedStruct>().Subject;
        var ipv4 = ethFrame.Children[3].Should().BeOfType<DecodedStruct>().Subject;

        // body is the last child of ipv4_packet (after virtual fields)
        var body = ipv4.Children.Last().Should().BeOfType<DecodedStruct>().Subject;
        body.Name.Should().Be("body");

        // tcp_segment fields
        var srcPort = body.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        srcPort.Name.Should().Be("src_port");
        srcPort.Value.Should().Be(80);

        var dstPort = body.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        dstPort.Name.Should().Be("dst_port");
        dstPort.Value.Should().Be(12345);
    }

    [Fact]
    public void PcapFormat_Ipv6_DecodesCorrectly()
    {
        var data = PcapTestDataGenerator.CreatePcapWithIpv6();
        var format = new YamlFormatLoader().Load(PcapFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var packets = decoded.Children[1].Should().BeOfType<DecodedArray>().Subject;
        var packet = packets.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        // data → ethernet_frame → payload → ipv6_packet
        var ethFrame = packet.Children[4].Should().BeOfType<DecodedStruct>().Subject;

        var etherType = ethFrame.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        etherType.Value.Should().Be(0x86DD);

        var ipv6 = ethFrame.Children[3].Should().BeOfType<DecodedStruct>().Subject;

        // payload_length
        var payloadLength = ipv6.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        payloadLength.Name.Should().Be("payload_length");
        payloadLength.Value.Should().Be(8);

        // next_header = 17 (UDP)
        var nextHeader = ipv6.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        nextHeader.Name.Should().Be("next_header");
        nextHeader.Value.Should().Be(17);
        nextHeader.EnumLabel.Should().Be("UDP");

        // body → udp_datagram
        var body = ipv6.Children.Last().Should().BeOfType<DecodedStruct>().Subject;
        body.Name.Should().Be("body");

        var srcPort = body.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        srcPort.Name.Should().Be("src_port");
        srcPort.Value.Should().Be(5353);
    }

    [Fact]
    public void PcapFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = PcapTestDataGenerator.CreateMinimalPcap();
        var format = new YamlFormatLoader().Load(PcapFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("PCAP");
        output.Should().Contain("header");
        output.Should().Contain("ETHERNET");
        output.Should().Contain("packets");
    }
}
