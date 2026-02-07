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
}
