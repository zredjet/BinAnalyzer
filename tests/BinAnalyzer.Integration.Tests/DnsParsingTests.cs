using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class DnsParsingTests
{
    private static readonly string DnsFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "dns.bdef.yaml");

    [Fact]
    public void DnsFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(DnsFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DnsFormat_DecodesMinimalDns()
    {
        var data = DnsTestDataGenerator.CreateMinimalDns();
        var format = new YamlFormatLoader().Load(DnsFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("DNS");
        decoded.Children.Should().HaveCount(2);
        decoded.Children[0].Name.Should().Be("header");
        decoded.Children[1].Name.Should().Be("payload");
    }

    [Fact]
    public void DnsFormat_Header_DecodesCorrectly()
    {
        var data = DnsTestDataGenerator.CreateMinimalDns();
        var format = new YamlFormatLoader().Load(DnsFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var header = decoded.Children[0].Should().BeOfType<DecodedStruct>().Subject;

        var txId = header.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        txId.Name.Should().Be("transaction_id");
        txId.Value.Should().Be(0x1234);

        var qdCount = header.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        qdCount.Name.Should().Be("qd_count");
        qdCount.Value.Should().Be(1);

        var anCount = header.Children[3].Should().BeOfType<DecodedInteger>().Subject;
        anCount.Name.Should().Be("an_count");
        anCount.Value.Should().Be(0);
    }

    [Fact]
    public void DnsFormat_Flags_DecodesCorrectly()
    {
        var data = DnsTestDataGenerator.CreateMinimalDns();
        var format = new YamlFormatLoader().Load(DnsFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var header = decoded.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        var flags = header.Children[1].Should().BeOfType<DecodedBitfield>().Subject;
        flags.Name.Should().Be("flags");
    }

    [Fact]
    public void DnsFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = DnsTestDataGenerator.CreateMinimalDns();
        var format = new YamlFormatLoader().Load(DnsFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("DNS");
        output.Should().Contain("header");
        output.Should().Contain("transaction_id");
        output.Should().Contain("qd_count");
        output.Should().Contain("payload");
    }
}
