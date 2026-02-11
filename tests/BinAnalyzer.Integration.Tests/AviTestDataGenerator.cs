using System.Buffers.Binary;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class AviTestDataGenerator
{
    /// <summary>
    /// 最小AVIファイル: RIFF(4B) + file_size(4B) + "AVI "(4B) + LIST chunk(8B+4B+"avih" sub-chunk(8B+56B)) = 88バイト
    /// RIFF/AVI コンテナ + hdrl LIST + avih ヘッダ(320x240, 30fps)
    /// </summary>
    public static byte[] CreateMinimalAvi()
    {
        var data = new byte[88];
        var span = data.AsSpan();
        var pos = 0;

        // === Root ===
        // magic: "RIFF"
        Encoding.ASCII.GetBytes("RIFF").CopyTo(span[pos..]); pos += 4;

        // file_size: 80 (total - 8)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 80); pos += 4;

        // form_type: "AVI "
        Encoding.ASCII.GetBytes("AVI ").CopyTo(span[pos..]); pos += 4;

        // === chunks: LIST chunk ===
        // chunk_id: "LIST"
        Encoding.ASCII.GetBytes("LIST").CopyTo(span[pos..]); pos += 4;

        // chunk_size: 64 (list_type(4) + avih chunk(8+52))
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 64); pos += 4;

        // data (switch -> list_chunk):
        // list_type: "hdrl"
        Encoding.ASCII.GetBytes("hdrl").CopyTo(span[pos..]); pos += 4;

        // sub_chunks: avih chunk
        // chunk_id: "avih"
        Encoding.ASCII.GetBytes("avih").CopyTo(span[pos..]); pos += 4;

        // chunk_size: 52 (we write the main_avi_header fields that fit in 52 bytes with remaining as reserved)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 52); pos += 4;

        // === main_avi_header (switch -> avih, 52 bytes boundary) ===
        // dwMicroSecPerFrame: 33333 (~30fps)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 33333); pos += 4;
        // dwMaxBytesPerSec: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        // dwPaddingGranularity: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        // dwFlags: 0x10 (AVIF_HASINDEX)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x10); pos += 4;
        // dwTotalFrames: 100
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 100); pos += 4;
        // dwInitialFrames: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        // dwStreams: 1
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1); pos += 4;
        // dwSuggestedBufferSize: 0
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        // dwWidth: 320
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 320); pos += 4;
        // dwHeight: 240
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 240); pos += 4;
        // dwReserved: 12 bytes remaining (we have 52 - 40 = 12 bytes left for 16-byte reserved, partial)
        // Actually: 10 fields * 4 = 40 bytes + 16 bytes reserved = 56 bytes for full header
        // But we set chunk_size to 52, so reserved is 12 bytes
        // The remaining bytes are already zeroed

        return data;
    }

    /// <summary>
    /// ストリームヘッダとインデックス付きAVI:
    /// RIFF(12) + LIST(hdrl: 8+4 + avih(8+56) + LIST(strl: 8+4 + strh(8+56))) + idx1(8+16) = 180バイト
    /// </summary>
    public static byte[] CreateAviWithStreamAndIndex()
    {
        var data = new byte[188];
        var span = data.AsSpan();
        var pos = 0;

        // === Root: RIFF ===
        Encoding.ASCII.GetBytes("RIFF").CopyTo(span[pos..]); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 180); pos += 4; // 188 - 8
        Encoding.ASCII.GetBytes("AVI ").CopyTo(span[pos..]); pos += 4;

        // === LIST hdrl ===
        Encoding.ASCII.GetBytes("LIST").CopyTo(span[pos..]); pos += 4;
        // chunk_size: 4(list_type) + 8+56(avih) + 8+4+8+56(strl LIST) = 144
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 144); pos += 4;
        Encoding.ASCII.GetBytes("hdrl").CopyTo(span[pos..]); pos += 4;

        // === avih chunk (8+56=64) ===
        Encoding.ASCII.GetBytes("avih").CopyTo(span[pos..]); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 56); pos += 4;
        // main_avi_header fields (56 bytes)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 33333); pos += 4; // dwMicroSecPerFrame
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;     // dwMaxBytesPerSec
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;     // dwPaddingGranularity
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x10); pos += 4;  // dwFlags
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 100); pos += 4;   // dwTotalFrames
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;     // dwInitialFrames
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1); pos += 4;     // dwStreams
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;     // dwSuggestedBufferSize
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 320); pos += 4;   // dwWidth
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 240); pos += 4;   // dwHeight
        pos += 16; // dwReserved (16 bytes zeroed)

        // === LIST strl ===
        Encoding.ASCII.GetBytes("LIST").CopyTo(span[pos..]); pos += 4;
        // chunk_size: 4(list_type) + 8+56(strh) = 68
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 68); pos += 4;
        Encoding.ASCII.GetBytes("strl").CopyTo(span[pos..]); pos += 4;

        // === strh chunk (8+56=64) ===
        Encoding.ASCII.GetBytes("strh").CopyTo(span[pos..]); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 56); pos += 4;
        // stream_header fields (56 bytes)
        Encoding.ASCII.GetBytes("vids").CopyTo(span[pos..]); pos += 4; // fccType
        Encoding.ASCII.GetBytes("H264").CopyTo(span[pos..]); pos += 4; // fccHandler
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;  // dwFlags
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;  // wPriority
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;  // wLanguage
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;  // dwInitialFrames
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1); pos += 4;  // dwScale
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 30); pos += 4; // dwRate
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;  // dwStart
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 100); pos += 4; // dwLength
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;  // dwSuggestedBufferSize
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;  // dwQuality
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;  // dwSampleSize
        pos += 8; // rcFrame (8 bytes zeroed)

        // === idx1 chunk (8+16=24) ===
        Encoding.ASCII.GetBytes("idx1").CopyTo(span[pos..]); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 16); pos += 4;
        // 1 index entry (16 bytes)
        Encoding.ASCII.GetBytes("00dc").CopyTo(span[pos..]); pos += 4;      // dwChunkId
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x10); pos += 4; // dwFlags (AVIIF_KEYFRAME)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 4); pos += 4;    // dwOffset
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1000);           // dwSize

        return data;
    }
}
