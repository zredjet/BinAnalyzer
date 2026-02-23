using System.Buffers.Binary;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class HeifTestDataGenerator
{
    /// <summary>
    /// 最小HEIFファイル:
    /// ftyp(24B) + meta(FullBox + hdlr + pitm + iloc + iinf(infe)) + mdat(16B)
    /// ISO Base Media File Format ベース、ビッグエンディアン
    /// </summary>
    public static byte[] CreateMinimalHeif()
    {
        // === サイズ計算（内側から外側へ） ===

        // ftyp: 8(header) + 4(major_brand) + 4(minor_version) + 8(compatible_brands) = 24
        const int ftypSize = 24;

        // hdlr: 8(header) + 4(ver+flags) + 4(pre_defined) + 4(handler_type) + 12(reserved) = 32
        const int hdlrSize = 32;

        // pitm: 8(header) + 4(ver+flags) + 2(item_id) = 14
        const int pitmSize = 14;

        // iloc: 8(header) + 4(ver+flags) + 2(size_info) + 2(item_count=0) = 16
        const int ilocSize = 16;

        // infe as iso_box: box_size(4) + box_type(4) + [ver(1) + flags(3) + item_id(2) + protection_index(2) + item_type(4)] = 20
        const int infeBoxSize = 20;

        // iinf: box_size(4) + box_type(4) + ver(1) + flags(3) + entry_count(2) + infeBox = 14 + infeBoxSize
        const int iinfBoxSize = 14 + infeBoxSize; // = 34

        // meta: box_size(4) + box_type(4) + ver(1) + flags(3) + hdlr + pitm + iloc + iinf
        const int metaSize = 8 + 4 + hdlrSize + pitmSize + ilocSize + iinfBoxSize;

        // mdat: 8(header) + 8(data) = 16
        const int mdatSize = 16;

        const int totalSize = ftypSize + metaSize + mdatSize;

        var data = new byte[totalSize];
        var span = data.AsSpan();
        var pos = 0;

        // === ftyp box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], ftypSize); pos += 4;
        Encoding.ASCII.GetBytes("ftyp").CopyTo(span[pos..]); pos += 4;
        Encoding.ASCII.GetBytes("heic").CopyTo(span[pos..]); pos += 4; // major_brand
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // minor_version
        Encoding.ASCII.GetBytes("heicmif1").CopyTo(span[pos..]); pos += 8; // compatible_brands

        // === meta box (FullBox) ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], (uint)metaSize); pos += 4;
        Encoding.ASCII.GetBytes("meta").CopyTo(span[pos..]); pos += 4;
        span[pos] = 0; pos += 1; // version
        pos += 3; // flags (zeroed)

        // === hdlr box (inside meta) ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], hdlrSize); pos += 4;
        Encoding.ASCII.GetBytes("hdlr").CopyTo(span[pos..]); pos += 4;
        span[pos] = 0; pos += 1; // version
        pos += 3; // flags
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // pre_defined
        Encoding.ASCII.GetBytes("pict").CopyTo(span[pos..]); pos += 4; // handler_type
        pos += 12; // reserved (zeroed)

        // === pitm box (inside meta) ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], (uint)pitmSize); pos += 4;
        Encoding.ASCII.GetBytes("pitm").CopyTo(span[pos..]); pos += 4;
        span[pos] = 0; pos += 1; // version
        pos += 3; // flags
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2; // item_id

        // === iloc box (inside meta) ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], (uint)ilocSize); pos += 4;
        Encoding.ASCII.GetBytes("iloc").CopyTo(span[pos..]); pos += 4;
        span[pos] = 0; pos += 1; // version
        pos += 3; // flags
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x4400); pos += 2; // size_info: offset_size=4, length_size=4, base_offset_size=0, index_size=0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2; // item_count=0

        // === iinf box (inside meta) ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], (uint)iinfBoxSize); pos += 4;
        Encoding.ASCII.GetBytes("iinf").CopyTo(span[pos..]); pos += 4;
        span[pos] = 0; pos += 1; // version
        pos += 3; // flags
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2; // entry_count

        // === infe box (inside iinf, as iso_box child) ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], (uint)infeBoxSize); pos += 4;
        Encoding.ASCII.GetBytes("infe").CopyTo(span[pos..]); pos += 4;
        span[pos] = 2; pos += 1; // version (2 for item_type field)
        pos += 3; // flags
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2; // item_id
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2; // item_protection_index
        Encoding.ASCII.GetBytes("hvc1").CopyTo(span[pos..]); pos += 4; // item_type

        // === mdat box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], mdatSize); pos += 4;
        Encoding.ASCII.GetBytes("mdat").CopyTo(span[pos..]); pos += 4;
        // 8 bytes dummy image data
        for (var i = 0; i < 8; i++) span[pos + i] = 0xFF;
        pos += 8;

        return data;
    }
}
