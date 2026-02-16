using System.Buffers.Binary;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class Mp4TestDataGenerator
{
    /// <summary>
    /// 最小MP4ファイル:
    /// ftyp(20B) + moov(mvhd + trak(tkhd + mdia(mdhd + hdlr + minf(smhd + dinf(dref) + stbl(stsd + stts + stsc + stsz + stco)))))
    /// ビッグエンディアン、box構造: size(4B) + type(4B) + data
    /// </summary>
    public static byte[] CreateMinimalMp4()
    {
        // === サイズ計算（内側から外側へ） ===

        // stco: 8(header) + 4(ver+flags) + 4(entry_count) + 4(chunk_offset) = 20
        const int stcoSize = 20;
        // stsz: 8(header) + 4(ver+flags) + 4(sample_size) + 4(sample_count) = 20
        //   sample_size=1 (non-zero) なので entry_sizes は省略
        const int stszSize = 20;
        // stsc: 8(header) + 4(ver+flags) + 4(entry_count) + 12(entry) = 28
        const int stscSize = 28;
        // stts: 8(header) + 4(ver+flags) + 4(entry_count) + 8(entry) = 24
        const int sttsSize = 24;
        // stsd: 8(header) + 4(ver+flags) + 4(entry_count) = 16
        //   entries は 0 バイト（entry_count=0 で remaining=0）
        const int stsdSize = 16;
        // stbl: 8(header) + children
        const int stblSize = 8 + stsdSize + sttsSize + stscSize + stszSize + stcoSize;

        // dref: 8(header) + 4(ver+flags) + 4(entry_count) = 16
        //   entries なし（remaining=0）
        const int drefSize = 16;
        // dinf: 8(header) + dref
        const int dinfSize = 8 + drefSize;

        // smhd: 8(header) + 4(ver+flags) + 2(balance) + 2(reserved) = 16
        const int smhdSize = 16;

        // minf: 8(header) + smhd + dinf + stbl
        const int minfSize = 8 + smhdSize + dinfSize + stblSize;

        // hdlr: 8(header) + 4(ver+flags) + 4(pre_defined) + 4(handler_type) + 12(reserved) = 32
        //   name なし（remaining=0）
        const int hdlrSize = 32;

        // mdhd: 8(header) + 4(ver+flags) + 4(creation_time) + 4(modification_time) + 4(timescale) + 4(duration) + 2(language) + 2(pre_defined) = 32
        const int mdhdSize = 32;

        // mdia: 8(header) + mdhd + hdlr + minf
        const int mdiaSize = 8 + mdhdSize + hdlrSize + minfSize;

        // tkhd: 8(header) + 4(ver+flags) + 4(creation) + 4(modification) + 4(track_id) + 4(reserved1) + 4(duration)
        //        + 8(reserved2) + 2(layer) + 2(alternate_group) + 2(volume) + 2(reserved3) + 36(matrix) + 4(width) + 4(height) = 92
        const int tkhdSize = 92;

        // trak: 8(header) + tkhd + mdia
        const int trakSize = 8 + tkhdSize + mdiaSize;

        // mvhd: 8(header) + 4(ver+flags) + 4(creation) + 4(modification) + 4(timescale) + 4(duration)
        //        + 4(rate) + 2(volume) + 10(reserved) + 36(matrix) + 24(pre_defined) + 4(next_track_id) = 108
        const int mvhdSize = 108;

        // moov: 8(header) + mvhd + trak
        const int moovSize = 8 + mvhdSize + trakSize;

        // ftyp: 8(header) + 4(major_brand) + 4(minor_version) + 8(compatible_brands) = 24
        const int ftypSize = 24;

        const int totalSize = ftypSize + moovSize;

        var data = new byte[totalSize];
        var span = data.AsSpan();
        var pos = 0;

        // === ftyp box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], ftypSize); pos += 4;
        Encoding.ASCII.GetBytes("ftyp").CopyTo(span[pos..]); pos += 4;
        Encoding.ASCII.GetBytes("isom").CopyTo(span[pos..]); pos += 4; // major_brand
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x200); pos += 4; // minor_version
        Encoding.ASCII.GetBytes("isomiso2").CopyTo(span[pos..]); pos += 8; // compatible_brands

        // === moov box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], (uint)moovSize); pos += 4;
        Encoding.ASCII.GetBytes("moov").CopyTo(span[pos..]); pos += 4;

        // === mvhd box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], mvhdSize); pos += 4;
        Encoding.ASCII.GetBytes("mvhd").CopyTo(span[pos..]); pos += 4;
        span[pos] = 0; pos += 1; // version
        pos += 3; // flags (zeroed)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // creation_time
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // modification_time
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1000); pos += 4; // timescale
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1000); pos += 4; // duration
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0x00010000); pos += 4; // rate (1.0)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x0100); pos += 2; // volume (1.0)
        pos += 10; // reserved (zeroed)
        // matrix: identity (3x3 fixed-point)
        WriteIdentityMatrix(span[pos..]); pos += 36;
        pos += 24; // pre_defined (zeroed)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 2); pos += 4; // next_track_id

        // === trak box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], (uint)trakSize); pos += 4;
        Encoding.ASCII.GetBytes("trak").CopyTo(span[pos..]); pos += 4;

        // === tkhd box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], tkhdSize); pos += 4;
        Encoding.ASCII.GetBytes("tkhd").CopyTo(span[pos..]); pos += 4;
        span[pos] = 0; pos += 1; // version
        // flags: track_enabled(0x01) | track_in_movie(0x02) = 0x03
        span[pos] = 0; pos += 1;
        span[pos] = 0; pos += 1;
        span[pos] = 0x03; pos += 1;
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // creation_time
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // modification_time
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4; // track_id
        pos += 4; // reserved1 (zeroed)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1000); pos += 4; // duration
        pos += 8; // reserved2 (zeroed)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2; // layer
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2; // alternate_group
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x0100); pos += 2; // volume (1.0 for audio)
        pos += 2; // reserved3 (zeroed)
        WriteIdentityMatrix(span[pos..]); pos += 36; // matrix
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // width (0 for audio)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // height (0 for audio)

        // === mdia box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], (uint)mdiaSize); pos += 4;
        Encoding.ASCII.GetBytes("mdia").CopyTo(span[pos..]); pos += 4;

        // === mdhd box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], mdhdSize); pos += 4;
        Encoding.ASCII.GetBytes("mdhd").CopyTo(span[pos..]); pos += 4;
        span[pos] = 0; pos += 1; // version
        pos += 3; // flags
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // creation_time
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // modification_time
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 44100); pos += 4; // timescale
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 44100); pos += 4; // duration
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x55C4); pos += 2; // language ("und")
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2; // pre_defined

        // === hdlr box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], hdlrSize); pos += 4;
        Encoding.ASCII.GetBytes("hdlr").CopyTo(span[pos..]); pos += 4;
        span[pos] = 0; pos += 1; // version
        pos += 3; // flags
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // pre_defined
        Encoding.ASCII.GetBytes("soun").CopyTo(span[pos..]); pos += 4; // handler_type
        pos += 12; // reserved (zeroed)

        // === minf box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], (uint)minfSize); pos += 4;
        Encoding.ASCII.GetBytes("minf").CopyTo(span[pos..]); pos += 4;

        // === smhd box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], smhdSize); pos += 4;
        Encoding.ASCII.GetBytes("smhd").CopyTo(span[pos..]); pos += 4;
        span[pos] = 0; pos += 1; // version
        pos += 3; // flags
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2; // balance
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2; // reserved

        // === dinf box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], (uint)dinfSize); pos += 4;
        Encoding.ASCII.GetBytes("dinf").CopyTo(span[pos..]); pos += 4;

        // === dref box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], drefSize); pos += 4;
        Encoding.ASCII.GetBytes("dref").CopyTo(span[pos..]); pos += 4;
        span[pos] = 0; pos += 1; // version
        pos += 3; // flags
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // entry_count=0

        // === stbl box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], (uint)stblSize); pos += 4;
        Encoding.ASCII.GetBytes("stbl").CopyTo(span[pos..]); pos += 4;

        // === stsd box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], stsdSize); pos += 4;
        Encoding.ASCII.GetBytes("stsd").CopyTo(span[pos..]); pos += 4;
        span[pos] = 0; pos += 1; // version
        pos += 3; // flags
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // entry_count=0

        // === stts box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], sttsSize); pos += 4;
        Encoding.ASCII.GetBytes("stts").CopyTo(span[pos..]); pos += 4;
        span[pos] = 0; pos += 1; // version
        pos += 3; // flags
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4; // entry_count
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4; // sample_count
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1024); pos += 4; // sample_delta

        // === stsc box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], stscSize); pos += 4;
        Encoding.ASCII.GetBytes("stsc").CopyTo(span[pos..]); pos += 4;
        span[pos] = 0; pos += 1; // version
        pos += 3; // flags
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4; // entry_count
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4; // first_chunk
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4; // samples_per_chunk
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4; // sample_description_index

        // === stsz box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], stszSize); pos += 4;
        Encoding.ASCII.GetBytes("stsz").CopyTo(span[pos..]); pos += 4;
        span[pos] = 0; pos += 1; // version
        pos += 3; // flags
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1024); pos += 4; // sample_size (non-zero = uniform)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4; // sample_count

        // === stco box ===
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], stcoSize); pos += 4;
        Encoding.ASCII.GetBytes("stco").CopyTo(span[pos..]); pos += 4;
        span[pos] = 0; pos += 1; // version
        pos += 3; // flags
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4; // entry_count
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 0); pos += 4; // chunk_offset

        return data;
    }

    /// <summary>
    /// 3x3 identity matrix を ISO BMFF 固定小数点形式で書き込む（36バイト）。
    /// { 0x00010000, 0, 0, 0, 0x00010000, 0, 0, 0, 0x40000000 }
    /// </summary>
    private static void WriteIdentityMatrix(Span<byte> dest)
    {
        // 9 values: a,b,u, c,d,v, x,y,w
        // a=1.0(16.16), b=0, u=0(2.30)
        // c=0, d=1.0(16.16), v=0(2.30)
        // x=0(16.16), y=0(16.16), w=1.0(2.30)
        BinaryPrimitives.WriteUInt32BigEndian(dest, 0x00010000);       // a = 1.0
        BinaryPrimitives.WriteUInt32BigEndian(dest[4..], 0);           // b = 0
        BinaryPrimitives.WriteUInt32BigEndian(dest[8..], 0);           // u = 0
        BinaryPrimitives.WriteUInt32BigEndian(dest[12..], 0);          // c = 0
        BinaryPrimitives.WriteUInt32BigEndian(dest[16..], 0x00010000); // d = 1.0
        BinaryPrimitives.WriteUInt32BigEndian(dest[20..], 0);          // v = 0
        BinaryPrimitives.WriteUInt32BigEndian(dest[24..], 0);          // x = 0
        BinaryPrimitives.WriteUInt32BigEndian(dest[28..], 0);          // y = 0
        BinaryPrimitives.WriteUInt32BigEndian(dest[32..], 0x40000000); // w = 1.0
    }
}
