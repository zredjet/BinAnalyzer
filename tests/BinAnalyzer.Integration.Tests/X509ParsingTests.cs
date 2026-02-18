using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class X509ParsingTests
{
    private static readonly string X509FormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "x509.bdef.yaml");

    private static DecodedNode Navigate(DecodedStruct root, params string[] path)
    {
        DecodedNode current = root;
        foreach (var name in path)
        {
            var s = (DecodedStruct)current;
            current = s.Children.First(c => c.Name == name);
        }
        return current;
    }

    [Fact]
    public void X509Format_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(X509FormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }


    [Fact]
    public void X509Format_DecodesMinimalCert()
    {
        var data = X509TestDataGenerator.CreateMinimalCertificate();
        var format = new YamlFormatLoader().Load(X509FormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("X509");
        decoded.Children.Should().NotBeEmpty();

        // certificate_content should contain tbs_certificate, sig_algorithm, signature
        var content = decoded.Children.First(c => c.Name == "content")
            .Should().BeOfType<DecodedStruct>().Subject;
        content.Children.Should().Contain(c => c.Name == "tbs_certificate");
        content.Children.Should().Contain(c => c.Name == "sig_algorithm");
        content.Children.Should().Contain(c => c.Name == "signature");
    }

    [Fact]
    public void X509Format_Version_DecodesCorrectly()
    {
        var data = X509TestDataGenerator.CreateMinimalCertificate();
        var format = new YamlFormatLoader().Load(X509FormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        // Navigate to version value: certificate > content > tbs > content > version_explicit > version_integer > value
        var versionValue = Navigate(decoded,
            "content", "tbs_certificate", "content", "version_explicit", "version_integer", "value");

        var versionInt = versionValue.Should().BeOfType<DecodedInteger>().Subject;
        versionInt.Value.Should().Be(2);
        versionInt.EnumLabel.Should().Be("v3");
    }

    [Fact]
    public void X509Format_Validity_DecodesCorrectly()
    {
        var data = X509TestDataGenerator.CreateMinimalCertificate();
        var format = new YamlFormatLoader().Load(X509FormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        // Navigate to validity content
        var validityContent = Navigate(decoded,
            "content", "tbs_certificate", "content", "validity", "content");
        var vc = (DecodedStruct)validityContent;

        var notBefore = Navigate(vc, "not_before", "value");
        notBefore.Should().BeOfType<DecodedString>().Which.Value.Should().Be("250101000000Z");

        var notAfter = Navigate(vc, "not_after", "value");
        notAfter.Should().BeOfType<DecodedString>().Which.Value.Should().Be("260101000000Z");
    }

    [Fact]
    public void X509Format_Issuer_ContainsRDN()
    {
        var data = X509TestDataGenerator.CreateMinimalCertificate();
        var format = new YamlFormatLoader().Load(X509FormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        // Navigate to issuer > content > rdn_sets (array) > first element > content > attrs > first > content > value
        var issuerContent = Navigate(decoded,
            "content", "tbs_certificate", "content", "issuer", "content");
        var ic = (DecodedStruct)issuerContent;

        // rdn_sets is an array
        var rdnSets = ic.Children.First(c => c.Name == "rdn_sets")
            .Should().BeOfType<DecodedArray>().Subject;
        rdnSets.Elements.Should().HaveCountGreaterThanOrEqualTo(1);

        // First RDN SET > content > attrs (array) > first element > content > value (ascii)
        var firstRdn = rdnSets.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var rdnContent = firstRdn.Children.First(c => c.Name == "content")
            .Should().BeOfType<DecodedStruct>().Subject;
        var attrs = rdnContent.Children.First(c => c.Name == "attrs")
            .Should().BeOfType<DecodedArray>().Subject;
        var firstAttr = attrs.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var attrContent = firstAttr.Children.First(c => c.Name == "content")
            .Should().BeOfType<DecodedStruct>().Subject;

        var attrValue = attrContent.Children.First(c => c.Name == "value")
            .Should().BeOfType<DecodedString>().Subject;
        attrValue.Value.Should().Be("Test");
    }

    [Fact]
    public void X509Format_TreeOutput_ContainsExpectedElements()
    {
        var data = X509TestDataGenerator.CreateMinimalCertificate();
        var format = new YamlFormatLoader().Load(X509FormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("certificate");
        output.Should().Contain("tbs_certificate");
        output.Should().Contain("v3");
        output.Should().Contain("SEQUENCE");
        output.Should().Contain("validity");
        output.Should().Contain("250101000000Z");
    }
}
