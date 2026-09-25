namespace BinAnalyzer.Integration.Tests;

/// <summary>
/// DER形式の最小X.509 v3証明書データを生成するヘルパー。
/// ボトムアップでTLV（Tag-Length-Value）を組み立てる。
/// </summary>
public static class X509TestDataGenerator
{
    /// <summary>
    /// ASN.1 TLV を構築する。Length は DER 可変長（short / 0x81 / 0x82）。
    /// </summary>
    private static byte[] Tlv(byte tag, byte[] value)
    {
        byte[] header;
        if (value.Length < 128)
        {
            header = [tag, (byte)value.Length];
        }
        else if (value.Length <= 255)
        {
            header = [tag, 0x81, (byte)value.Length];
        }
        else
        {
            header = [tag, 0x82, (byte)(value.Length >> 8), (byte)(value.Length & 0xFF)];
        }

        var result = new byte[header.Length + value.Length];
        header.CopyTo(result, 0);
        value.CopyTo(result, header.Length);
        return result;
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var total = 0;
        foreach (var p in parts) total += p.Length;
        var result = new byte[total];
        var offset = 0;
        foreach (var p in parts)
        {
            p.CopyTo(result, offset);
            offset += p.Length;
        }
        return result;
    }

    private static byte[] Ascii(string s) => System.Text.Encoding.ASCII.GetBytes(s);

    /// <summary>
    /// 最小 v3 自己署名証明書（DER形式）。
    /// Version=v3, Serial=1, Issuer=CN=Test, Subject=CN=Test,
    /// Validity=2025-2026 UTCTime, SPKI=Ed25519 の形（鍵は 01〜20 の 32 バイト）, Extensions=なし,
    /// Signature=ダミー。
    /// </summary>
    public static byte[] CreateMinimalCertificate()
    {
        // OID: CN (2.5.4.3)
        byte[] cnOid = [0x55, 0x04, 0x03];
        // OID: sha256WithRSAEncryption (1.2.840.113549.1.1.11)
        byte[] sha256RsaOid = [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x0B];

        // Version: v3 (integer value = 2)
        var version = Tlv(0xA0, Tlv(0x02, [0x02]));

        // Serial Number: 1
        var serial = Tlv(0x02, [0x01]);

        // Signature Algorithm: sha256WithRSA
        var sigAlg = Tlv(0x30, Tlv(0x06, sha256RsaOid));

        // Issuer: CN=Test
        var rdnAttr = Tlv(0x30, Concat(Tlv(0x06, cnOid), Tlv(0x13, Ascii("Test"))));
        var rdnSet = Tlv(0x31, rdnAttr);
        var issuer = Tlv(0x30, rdnSet);

        // Validity: UTCTime
        var notBefore = Tlv(0x17, Ascii("250101000000Z"));
        var notAfter = Tlv(0x17, Ascii("260101000000Z"));
        var validity = Tlv(0x30, Concat(notBefore, notAfter));

        // Subject: CN=Test (same as issuer)
        var subject = Tlv(0x30, rdnSet);

        // Subject Public Key Info: Ed25519（1.3.101.112）+ 32 バイトの鍵（REQ-188 で正しい形に直した。以前は中身が DE AD BE EF だけだった）
        var spki = Tlv(0x30, Concat(Tlv(0x30, Tlv(0x06, [0x2B, 0x65, 0x70])), Tlv(0x03, Concat([0x00], Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()))));

        // TBS Certificate
        var tbs = Tlv(0x30, Concat(version, serial, sigAlg, issuer, validity, subject, spki));

        // Outer Signature Algorithm
        var outerSigAlg = Tlv(0x30, Tlv(0x06, sha256RsaOid));

        // Signature Value: BIT STRING (unused_bits=0 + dummy)
        var sigValue = Tlv(0x03, Concat([0x00], [0xAA, 0xBB, 0xCC, 0xDD]));

        // Certificate SEQUENCE
        return Tlv(0x30, Concat(tbs, outerSigAlg, sigValue));
    }

    // ===== REQ-188: 版 1・拡張・RSA の鍵 =====
    private static byte[] Oid(params byte[] encoded) => Tlv(0x06, encoded);

    private static byte[] Name(string cn, string? o = null, string? c = null)
    {
        var rdns = new List<byte[]>();
        if (c is not null) rdns.Add(Tlv(0x31, Tlv(0x30, Concat(Oid(0x55, 0x04, 0x06), Tlv(0x13, Ascii(c))))));
        if (o is not null) rdns.Add(Tlv(0x31, Tlv(0x30, Concat(Oid(0x55, 0x04, 0x0A), Tlv(0x0C, System.Text.Encoding.UTF8.GetBytes(o))))));
        rdns.Add(Tlv(0x31, Tlv(0x30, Concat(Oid(0x55, 0x04, 0x03), Tlv(0x0C, System.Text.Encoding.UTF8.GetBytes(cn))))));
        return Tlv(0x30, Concat(rdns.ToArray()));
    }

    private static readonly byte[] Sha256Rsa = Tlv(0x30, Concat(Oid(0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x0B), Tlv(0x05, [])));

    private static byte[] RsaSpki()
    {
        var modulus = new byte[65];
        modulus[1] = 0xC0;
        for (var i = 2; i < 65; i++) modulus[i] = (byte)(i * 7);
        modulus[64] |= 1;
        var key = Tlv(0x30, Concat(Tlv(0x02, modulus), Tlv(0x02, [0x01, 0x00, 0x01])));
        var algorithm = Tlv(0x30, Concat(Oid(0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01), Tlv(0x05, [])));
        return Tlv(0x30, Concat(algorithm, Tlv(0x03, Concat([0x00], key))));
    }

    private static byte[] Certificate(byte[] tbsBody) =>
        Tlv(0x30, Concat(Tlv(0x30, tbsBody), Sha256Rsa, Tlv(0x03, Concat([0x00], new byte[64]))));

    /// <summary>
    /// 版 1 の証明書（版の [0] を省く、REQ-188）。シリアル番号 0x0123456789ABCDEF0123（10 バイト）、発行者 = 主体 = C=JP, O=テスト, CN=v1.example、
    /// GeneralizedTime の有効期間、RSA の鍵（512 ビット、e = 65537）、拡張なし。openssl x509 -inform der -text で読めることを確かめた。
    /// </summary>
    public static byte[] CreateV1Certificate()
    {
        var name = Name("v1.example", "テスト", "JP");
        var validity = Tlv(0x30, Concat(Tlv(0x18, Ascii("20250101000000Z")), Tlv(0x18, Ascii("20491231235959Z"))));
        return Certificate(Concat(Tlv(0x02, [0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF, 0x01, 0x23]), Sha256Rsa, name, validity, name, RsaSpki()));
    }

    /// <summary>
    /// 拡張付きの版 3 の証明書（REQ-188）。basicConstraints（重要、CA = 真、pathLen = 0）・keyUsage（digitalSignature・keyCertSign）・
    /// extKeyUsage（serverAuth・clientAuth）・subjectAltName（dNSName 2 つ・iPAddress・rfc822Name）・subjectKeyIdentifier。
    /// openssl x509 -inform der -text が同じ拡張を表示することを確かめた。
    /// </summary>
    public static byte[] CreateCertificateWithExtensions()
    {
        static byte[] Ext(byte[] oid, byte[] value, bool critical = false) =>
            Tlv(0x30, Concat(Tlv(0x06, oid), critical ? Tlv(0x01, [0xFF]) : [], Tlv(0x04, value)));
        var extensions = Tlv(0xA3, Tlv(0x30, Concat(
            Ext([0x55, 0x1D, 0x13], Tlv(0x30, Concat(Tlv(0x01, [0xFF]), Tlv(0x02, [0x00]))), critical: true),
            Ext([0x55, 0x1D, 0x0F], Tlv(0x03, [0x02, 0x84])),
            Ext([0x55, 0x1D, 0x25], Tlv(0x30, Concat(Oid(0x2B, 0x06, 0x01, 0x05, 0x05, 0x07, 0x03, 0x01), Oid(0x2B, 0x06, 0x01, 0x05, 0x05, 0x07, 0x03, 0x02)))),
            Ext([0x55, 0x1D, 0x11], Tlv(0x30, Concat(Tlv(0x82, Ascii("example.com")), Tlv(0x82, Ascii("*.example.com")), Tlv(0x87, [192, 0, 2, 1]), Tlv(0x81, Ascii("a@example.com"))))),
            Ext([0x55, 0x1D, 0x0E], Tlv(0x04, Enumerable.Range(0, 20).Select(i => (byte)(0xA0 + i)).ToArray())))));
        var validity = Tlv(0x30, Concat(Tlv(0x17, Ascii("250101000000Z")), Tlv(0x17, Ascii("351231235959Z"))));
        return Certificate(Concat(Tlv(0xA0, Tlv(0x02, [0x02])), Tlv(0x02, [0x2A]), Sha256Rsa, Name("ca.example"), validity, Name("ca.example"), RsaSpki(), extensions));
    }
}
