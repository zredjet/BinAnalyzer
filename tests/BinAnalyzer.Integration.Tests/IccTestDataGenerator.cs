using System.Buffers.Binary;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class IccTestDataGenerator
{
    /// <summary>
    /// 最小ICCプロファイル: profile_header(128B) + tag_table(4B + 0 tags) = 132バイト
    /// Monitor, RGB, Perceptual, "acsp" signature
    /// </summary>
    public static byte[] CreateMinimalIcc()
    {
        var data = new byte[132];
        var span = data.AsSpan();
        var pos = 0;

        // === profile_header (128 bytes) ===

        // profile_size: 132
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 132); pos += 4;

        // preferred_cmm: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // version: 4.0.0 (4 bytes)
        data[pos] = 0x04; data[pos + 1] = 0x00; pos += 4;

        // device_class: Monitor (0x6D6E7472 = "mntr")
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x6D6E7472); pos += 4;

        // color_space: RGB (0x52474220)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x52474220); pos += 4;

        // pcs: "XYZ " (4 bytes ASCII)
        Encoding.ASCII.GetBytes("XYZ ").CopyTo(span[pos..]); pos += 4;

        // creation_date: 12 bytes (zeroed)
        pos += 12;

        // signature: "acsp" (expected)
        Encoding.ASCII.GetBytes("acsp").CopyTo(span[pos..]); pos += 4;

        // primary_platform: Microsoft (0x4D534654 = "MSFT")
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x4D534654); pos += 4;

        // flags: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // device_manufacturer: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // device_model: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // attributes: 8 bytes = 0
        BinaryPrimitives.WriteUInt64BigEndian(span[pos..], 0); pos += 8;

        // rendering_intent: 0 (Perceptual)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // pcs_illuminant: D50 standard (12 bytes)
        // X = 0.9642 (s15Fixed16 = 0x0000F6D6)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x0000F6D6); pos += 4;
        // Y = 1.0000 (s15Fixed16 = 0x00010000)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x00010000); pos += 4;
        // Z = 0.8249 (s15Fixed16 = 0x0000D32D)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x0000D32D); pos += 4;

        // creator: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4;

        // id: 16 bytes = 0
        pos += 16;

        // reserved: 28 bytes = 0
        pos += 28;

        // pos should now be 128

        // === tag_table ===
        // tag_count: 0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0);

        return data;
    }

    /// <summary>
    /// タグ付きICCプロファイル:
    /// profile_header(128B) + tag_table(4B + 2*12B = 28B) + desc_tag(40B) + xyz_tag(20B) = 216バイト
    /// </summary>
    public static byte[] CreateIccWithTags()
    {
        var data = new byte[216];
        var span = data.AsSpan();
        var pos = 0;

        // === profile_header (128 bytes) — same as minimal but with updated size ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 216); pos += 4; // profile_size
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // preferred_cmm
        data[pos] = 0x04; data[pos + 1] = 0x00; pos += 4; // version 4.0.0
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x6D6E7472); pos += 4; // Monitor
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x52474220); pos += 4; // RGB
        Encoding.ASCII.GetBytes("XYZ ").CopyTo(span[pos..]); pos += 4; // pcs
        pos += 12; // creation_date
        Encoding.ASCII.GetBytes("acsp").CopyTo(span[pos..]); pos += 4; // signature
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x4D534654); pos += 4; // MSFT
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // flags
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // device_manufacturer
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // device_model
        BinaryPrimitives.WriteUInt64BigEndian(span[pos..], 0); pos += 8; // attributes
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // rendering_intent
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x0000F6D6); pos += 4; // pcs X
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x00010000); pos += 4; // pcs Y
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x0000D32D); pos += 4; // pcs Z
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // creator
        pos += 16; // id
        pos += 28; // reserved
        // pos = 128

        // === tag_table ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 2); pos += 4; // tag_count=2

        // tag 1: desc, offset=156, size=40
        Encoding.ASCII.GetBytes("desc").CopyTo(span[pos..]); pos += 4;
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 156); pos += 4; // offset
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 40); pos += 4; // size

        // tag 2: XYZ , offset=196, size=20
        Encoding.ASCII.GetBytes("XYZ ").CopyTo(span[pos..]); pos += 4;
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 196); pos += 4; // offset
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 20); pos += 4; // size
        // pos = 156

        // === desc_tag_data at offset 156 (40 bytes) ===
        Encoding.ASCII.GetBytes("desc").CopyTo(span[pos..]); pos += 4; // type_signature
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // reserved
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 12); pos += 4; // ascii_length=12
        Encoding.ASCII.GetBytes("Test Profile").CopyTo(span[pos..]); pos += 12; // ascii_description
        pos += 16; // extra_data (remaining = 40-4-4-4-12 = 16)
        // pos = 196

        // === xyz_tag_data at offset 196 (20 bytes) ===
        Encoding.ASCII.GetBytes("XYZ ").CopyTo(span[pos..]); pos += 4; // type_signature
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // reserved
        BinaryPrimitives.WriteInt32BigEndian(span[pos..], 0x0000F6D6); pos += 4; // x
        BinaryPrimitives.WriteInt32BigEndian(span[pos..], 0x00010000); pos += 4; // y
        BinaryPrimitives.WriteInt32BigEndian(span[pos..], 0x0000D32D); // z

        return data;
    }

    /// <summary>
    /// v4 のプロファイル（REQ-188）。タグ 'desc'（mluc 型、en-US "Test v4"）、'rTRC'（para 型、関数 0: g = 2.2）、'chad'（sf32 型、3x3）。
    /// </summary>
    public static byte[] CreateV4ProfileWithTypedTags()
    {
        byte[] Be32(uint v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];
        byte[] Be16(ushort v) => [(byte)(v >> 8), (byte)v];
        var text = Encoding.BigEndianUnicode.GetBytes("Test v4");
        byte[] mluc = [.. "mluc"u8, 0, 0, 0, 0, .. Be32(1), .. Be32(12), .. "en"u8, .. "US"u8, .. Be32((uint)text.Length), .. Be32(28), .. text];
        byte[] para = [.. "para"u8, 0, 0, 0, 0, .. Be16(0), 0, 0, .. Be32(0x00023333)];
        byte[] sf32 = [.. "sf32"u8, 0, 0, 0, 0, .. Enumerable.Range(0, 9).SelectMany(i => Be32(i % 4 == 0 ? 0x00010000u : 0u))];
        var tagData = new List<(string Sig, byte[] Data)> { ("desc", mluc), ("rTRC", para), ("chad", sf32) };

        const int headerSize = 128;
        var tableSize = 4 + tagData.Count * 12;
        var offsets = new List<int>();
        var pos = headerSize + tableSize;
        foreach (var (_, d) in tagData)
        {
            offsets.Add(pos);
            pos += (d.Length + 3) / 4 * 4;
        }
        var data = new byte[pos];
        Be32((uint)pos).CopyTo(data, 0);                   // プロファイルサイズ
        Be32(0x04400000).CopyTo(data, 8);                  // バージョン 4.4
        "mntr"u8.ToArray().CopyTo(data, 12);
        "RGB "u8.ToArray().CopyTo(data, 16);
        "XYZ "u8.ToArray().CopyTo(data, 20);
        "acsp"u8.ToArray().CopyTo(data, 36);
        Be32((uint)tagData.Count).CopyTo(data, headerSize);
        for (var i = 0; i < tagData.Count; i++)
        {
            var entry = headerSize + 4 + i * 12;
            Encoding.ASCII.GetBytes(tagData[i].Sig).CopyTo(data, entry);
            Be32((uint)offsets[i]).CopyTo(data, entry + 4);
            Be32((uint)tagData[i].Data.Length).CopyTo(data, entry + 8);
            tagData[i].Data.CopyTo(data, offsets[i]);
        }
        return data;
    }
}
