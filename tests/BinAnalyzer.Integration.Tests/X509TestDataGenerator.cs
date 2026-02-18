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
    /// Validity=2025-2026 UTCTime, SPKI=ダミー, Extensions=なし,
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

        // Subject Public Key Info (dummy)
        var spki = Tlv(0x30, [0xDE, 0xAD, 0xBE, 0xEF]);

        // TBS Certificate
        var tbs = Tlv(0x30, Concat(version, serial, sigAlg, issuer, validity, subject, spki));

        // Outer Signature Algorithm
        var outerSigAlg = Tlv(0x30, Tlv(0x06, sha256RsaOid));

        // Signature Value: BIT STRING (unused_bits=0 + dummy)
        var sigValue = Tlv(0x03, Concat([0x00], [0xAA, 0xBB, 0xCC, 0xDD]));

        // Certificate SEQUENCE
        return Tlv(0x30, Concat(tbs, outerSigAlg, sigValue));
    }
}
