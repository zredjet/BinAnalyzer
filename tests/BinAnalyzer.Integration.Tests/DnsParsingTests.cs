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
        var result = FormatValidator.Validate(new YamlFormatLoader().Load(DnsFormatPath));
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void DnsFormat_DecodesMinimalQuery()
    {
        var decoded = Decode(DnsTestDataGenerator.CreateMinimalDns());

        decoded.Children.Select(c => c.Name).Should().Equal("header", "questions", "answers", "authorities", "additionals");
        var header = decoded.Child("header");
        header.Child("transaction_id").Int().Should().Be(0x1234);
        header.Child("flags").Bits("qr").Should().Be(0);
        header.Child("flags").Bits("rd").Should().Be(1);
        header.Child("qd_count").Int().Should().Be(1);
        var question = decoded.Child("questions").Elements().Single();
        question.Child("qname").Child("full_name").Str().Should().Be("www.example.com.");
        question.Child("qtype").Label().Should().Be("A");
        ((DecodedBitfield)question.Child("qclass_field")).Fields.Should().Contain(f => f.Name == "qclass" && f.EnumLabel == "IN");
    }

    [Fact]
    public void DnsFormat_CompressionPointers_ResolveFullNames()
    {
        var decoded = Decode(DnsTestDataGenerator.CreateDnsResponse());

        var answers = decoded.Child("answers").Elements();
        answers.Should().HaveCount(6);
        answers.Select(a => a.Child("name").Child("full_name").Str()).Should().Equal(
            "www.example.com.", "www.example.com.", "example.com.", "example.com.", "cdn.example.com.", "example.com.");
        // ラベル + ポインタの名前（mail → example.com）
        var mx = answers[2].Child("body").Child("rdata");
        mx.Child("preference").Int().Should().Be(10);
        mx.Child("exchange").Child("full_name").Str().Should().Be("mail.example.com.");
        // ポインタが指す名前の中のポインタ（cdn → example.com、CNAME の中の www → example.com）
        answers[4].Child("body").Child("rdata").Child("target").Child("full_name").Str().Should().Be("www.example.com.");
    }

    [Fact]
    public void DnsFormat_ResourceRecords_DecodeRdataByType()
    {
        var decoded = Decode(DnsTestDataGenerator.CreateDnsResponse());
        var answers = decoded.Child("answers").Elements();

        answers[0].Child("type").Label().Should().Be("A");
        answers[0].Child("body").Child("ttl").Int().Should().Be(300);
        answers[0].Child("body").Child("rdata").Child("address").Str().Should().Be("93.184.216.34");
        answers[1].Child("body").Child("rdata").Child("address").Str().Should().Be("2606:2800:220:1:248:1893:25c8:1946");
        answers[3].Child("body").Child("rdata").Child("strings").Elements().Select(e => e.Child("text").Str())
            .Should().Equal("v=spf1 -all", "hello");
        var caa = answers[5].Child("body").Child("rdata");
        caa.Child("tag").Str().Should().Be("issue");
        caa.Child("value").Str().Should().Be("letsencrypt.org");

        var soa = decoded.Child("authorities").Elements().Single().Child("body").Child("rdata");
        soa.Child("mname").Child("full_name").Str().Should().Be("ns.example.com.");
        soa.Child("rname").Child("full_name").Str().Should().Be("admin.example.com.");
        soa.Child("serial").Int().Should().Be(2026092501);
        soa.Child("minimum").Int().Should().Be(300);
    }

    [Fact]
    public void DnsFormat_OptRecord_DecodesEdns()
    {
        var decoded = Decode(DnsTestDataGenerator.CreateDnsResponse());

        var opt = decoded.Child("additionals").Elements().Single();
        opt.Child("name").Child("full_name").Str().Should().Be(".");
        opt.Child("type").Label().Should().Be("OPT");
        var body = opt.Child("body");
        ((DecodedStruct)body).StructType.Should().Be("opt_record");
        body.Child("udp_payload_size").Int().Should().Be(1232);
        body.Child("edns_flags").Bits("do").Should().Be(1);
        var option = body.Child("options").Elements().Single();
        option.Child("code").Label().Should().Be("COOKIE");
        option.Child("length").Int().Should().Be(8);
    }

    [Fact]
    public void DnsFormat_TreeOutput_ContainsExpectedElements()
    {
        var output = new TreeOutputFormatter().Format(Decode(DnsTestDataGenerator.CreateMinimalDns()));

        output.Should().Contain("DNS");
        output.Should().Contain("transaction_id");
        output.Should().Contain("www.example.com.");
        output.Should().NotContain("name_acc");   // 作業用の値は表示しない
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(DnsFormatPath));
}
