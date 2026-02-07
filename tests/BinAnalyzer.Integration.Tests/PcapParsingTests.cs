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
        inclLen.Value.Should().Be(34);
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
