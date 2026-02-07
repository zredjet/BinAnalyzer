using System.Buffers.Binary;

namespace BinAnalyzer.Integration.Tests;

public static class DnsTestDataGenerator
{
    /// <summary>
    /// 最小DNSパケット: header(12B) + payload(4B) = 16バイト
    /// 標準クエリ、1 question、再帰要求ありのDNSヘッダ
    /// </summary>
    public static byte[] CreateMinimalDns()
    {
        var data = new byte[16];
        var span = data.AsSpan();
        var pos = 0;

        // transaction_id: 0x1234
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x1234); pos += 2;

        // flags: 2 bytes (bitfield)
        // qr=0 (query), opcode=0, aa=0, tc=0, rd=1, ra=0, z=0, rcode=0 (NoError)
        // Bit layout (MSB first): qr(1) opcode(4) aa(1) tc(1) rd(1) ra(1) z(3) rcode(4)
        // = 0 0000 0 0 1  0 000 0000 = 0x0100
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x0100); pos += 2;

        // qd_count: 1
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2;

        // an_count: 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // ns_count: 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // ar_count: 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // payload: dummy question data
        data[12] = 0x00; // root label
        data[13] = 0x00;
        data[14] = 0x01; // type A
        data[15] = 0x01; // class IN (partial)

        return data;
    }
}
