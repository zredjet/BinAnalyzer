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

    [Fact]
    public void X509Format_LoadsWithoutErrors()
    {
        var result = FormatValidator.Validate(new YamlFormatLoader().Load(X509FormatPath));
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void X509Format_MinimalCertificate_DecodesTbsFields()
    {
        var tbs = Certificate(X509TestDataGenerator.CreateMinimalCertificate()).Child("tbs_certificate");

        tbs.Child("version").Child("version").Label().Should().Be("v3");
        tbs.Child("serial_number").Find("int_value").Int().Should().Be(1);
        tbs.Child("signature").Find("name").Str().Should().Be("sha256WithRSAEncryption");
        Oids(tbs.Child("issuer")).Should().Equal("commonName");
        tbs.Child("issuer").FindAll("text").Select(t => t.Str()).Should().Equal("Test");
        tbs.Child("validity").FindAll("text").Select(t => t.Str()).Should().Equal("250101000000Z", "260101000000Z");
        tbs.Child("subject_public_key_info").Child("algorithm").Find("name").Str().Should().Be("Ed25519");
        tbs.Child("subject_public_key_info").Child("public_key_bytes").Size.Should().Be(32);
    }

    [Fact]
    public void X509Format_V1Certificate_HasNoVersionField()
    {
        var tbs = Certificate(X509TestDataGenerator.CreateV1Certificate()).Child("tbs_certificate");

        ((DecodedStruct)tbs).Children.Should().NotContain(c => c.Name == "version" || c.Name == "extensions");
        ((DecodedBytes)tbs.Child("serial_number").Find("int_bytes")).RawBytes.ToArray().Should().Equal(0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF, 0x01, 0x23);
        Oids(tbs.Child("subject")).Should().Equal("countryName", "organizationName", "commonName");
        tbs.Child("subject").FindAll("text").Select(t => t.Str()).Should().Equal("JP", "テスト", "v1.example");
        tbs.Child("validity").FindAll("type_name").Select(t => t.Label()).Should().Contain("GeneralizedTime");
    }

    [Fact]
    public void X509Format_RsaKey_DecodesModulusAndExponent()
    {
        var spki = Certificate(X509TestDataGenerator.CreateV1Certificate()).Child("tbs_certificate").Child("subject_public_key_info");

        spki.Child("algorithm").Find("name").Str().Should().Be("rsaEncryption");
        var key = spki.Child("public_key").Child("content").Child("items").Elements();
        key[0].Find("int_bytes").Size.Should().Be(65);
        key[1].Find("int_value").Int().Should().Be(65537);
    }

    [Fact]
    public void X509Format_Extensions_DecodeInnerDer()
    {
        var extensions = Certificate(X509TestDataGenerator.CreateCertificateWithExtensions())
            .Child("tbs_certificate").Child("extensions").Child("extensions").Elements();

        extensions.Select(e => e.Child("name").Str()).Should().Equal(
            "basicConstraints", "keyUsage", "extKeyUsage", "subjectAltName", "subjectKeyIdentifier");
        var basic = extensions[0];
        basic.Child("critical").Find("bool_value").Int().Should().Be(0xFF);
        basic.Child("extn_value").Find("bool_value").Int().Should().Be(0xFF);  // cA
        basic.Child("extn_value").Find("int_value").Int().Should().Be(0);      // pathLenConstraint
        ((DecodedStruct)extensions[1]).Children.Should().NotContain(c => c.Name == "critical");
        extensions[2].Child("extn_value").FindAll("name").Select(n => n.Str()).Should().Equal("serverAuth", "clientAuth");
        extensions[3].Child("extn_value").FindAll("text").Select(t => t.Str()).Should().Equal("example.com", "*.example.com", "a@example.com");
        extensions[3].Child("extn_value").FindAll("context_tag").Select(t => t.Str()).Should().Equal("[2]", "[2]", "[7]", "[1]");
        extensions[4].Child("extn_value").Find("data").Size.Should().Be(20);
    }

    [Fact]
    public void X509Format_ConcatenatedCertificates_AreReadInOrder()
    {
        var data = X509TestDataGenerator.CreateMinimalCertificate().Concat(X509TestDataGenerator.CreateV1Certificate()).ToArray();
        var certificates = Decode(data).Child("certificates").Elements();

        certificates.Should().HaveCount(2);
        certificates[1].Offset.Should().Be(X509TestDataGenerator.CreateMinimalCertificate().Length);
    }

    [Fact]
    public void X509Format_TreeOutput_ContainsExpectedElements()
    {
        var output = new TreeOutputFormatter().Format(Decode(X509TestDataGenerator.CreateCertificateWithExtensions()));

        output.Should().Contain("tbs_certificate");
        output.Should().Contain("subjectAltName");
        output.Should().Contain("2.5.29.17");
        output.Should().NotContain("next_tag");
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(X509FormatPath));

    private static DecodedNode Certificate(byte[] data) => Decode(data).Child("certificates").Elements().Single();

    /// <summary>名前の中の属性の OID の名前（RDN の順）。</summary>
    private static List<string> Oids(DecodedNode name) =>
        name.OfStruct("der_oid").Select(o => o.Child("name").Str()).ToList();
}
