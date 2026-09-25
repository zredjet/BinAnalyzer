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

    /// <summary>
    /// 映像ストリームフォーマット付きAVI:
    /// RIFF(12) + LIST(hdrl: avih(64) + LIST(strl: strh(64) + strf(48))) = 220バイト
    /// strf内にBITMAPINFOHEADER（40バイト）
    /// </summary>
    public static byte[] CreateAviWithVideoStreamFormat()
    {
        var data = new byte[220];
        var span = data.AsSpan();
        var pos = 0;

        // === Root: RIFF ===
        Encoding.ASCII.GetBytes("RIFF").CopyTo(span[pos..]); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 212); pos += 4; // 220 - 8
        Encoding.ASCII.GetBytes("AVI ").CopyTo(span[pos..]); pos += 4;

        // === LIST hdrl ===
        Encoding.ASCII.GetBytes("LIST").CopyTo(span[pos..]); pos += 4;
        // chunk_size: 4(list_type) + 64(avih) + 8+4+64+48(strl LIST) = 192
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 192); pos += 4;
        Encoding.ASCII.GetBytes("hdrl").CopyTo(span[pos..]); pos += 4;

        // === avih chunk (8+56=64) ===
        Encoding.ASCII.GetBytes("avih").CopyTo(span[pos..]); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 56); pos += 4;
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
        pos += 16; // dwReserved

        // === LIST strl ===
        Encoding.ASCII.GetBytes("LIST").CopyTo(span[pos..]); pos += 4;
        // chunk_size: 4(list_type) + 64(strh) + 48(strf) = 116
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 116); pos += 4;
        Encoding.ASCII.GetBytes("strl").CopyTo(span[pos..]); pos += 4;

        // === strh chunk (8+56=64) ===
        Encoding.ASCII.GetBytes("strh").CopyTo(span[pos..]); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 56); pos += 4;
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
        pos += 8; // rcFrame

        // === strf chunk (8+40=48) ===
        Encoding.ASCII.GetBytes("strf").CopyTo(span[pos..]); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 40); pos += 4;
        // BITMAPINFOHEADER (40 bytes)
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 40); pos += 4;   // biSize
        BinaryPrimitives.WriteInt32LittleEndian(span[pos..], 320); pos += 4;   // biWidth
        BinaryPrimitives.WriteInt32LittleEndian(span[pos..], 240); pos += 4;   // biHeight
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 1); pos += 2;    // biPlanes
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 24); pos += 2;   // biBitCount
        Encoding.ASCII.GetBytes("H264").CopyTo(span[pos..]); pos += 4;        // biCompression
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 230400); pos += 4; // biSizeImage (320*240*3)
        BinaryPrimitives.WriteInt32LittleEndian(span[pos..], 0); pos += 4;     // biXPelsPerMeter
        BinaryPrimitives.WriteInt32LittleEndian(span[pos..], 0); pos += 4;     // biYPelsPerMeter
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;    // biClrUsed
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0);              // biClrImportant

        return data;
    }

    /// <summary>
    /// 音声ストリームフォーマット付きAVI:
    /// RIFF(12) + LIST(hdrl: avih(64) + LIST(strl: strh(64) + strf(24))) = 196バイト
    /// strf内にWAVEFORMATEX（16バイト）
    /// </summary>
    public static byte[] CreateAviWithAudioStreamFormat()
    {
        var data = new byte[196];
        var span = data.AsSpan();
        var pos = 0;

        // === Root: RIFF ===
        Encoding.ASCII.GetBytes("RIFF").CopyTo(span[pos..]); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 188); pos += 4; // 196 - 8
        Encoding.ASCII.GetBytes("AVI ").CopyTo(span[pos..]); pos += 4;

        // === LIST hdrl ===
        Encoding.ASCII.GetBytes("LIST").CopyTo(span[pos..]); pos += 4;
        // chunk_size: 4(list_type) + 64(avih) + 8+4+64+24(strl LIST) = 168
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 168); pos += 4;
        Encoding.ASCII.GetBytes("hdrl").CopyTo(span[pos..]); pos += 4;

        // === avih chunk (8+56=64) ===
        Encoding.ASCII.GetBytes("avih").CopyTo(span[pos..]); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 56); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 33333); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0x10); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 100); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;
        pos += 16; // dwReserved

        // === LIST strl ===
        Encoding.ASCII.GetBytes("LIST").CopyTo(span[pos..]); pos += 4;
        // chunk_size: 4(list_type) + 64(strh) + 24(strf) = 92
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 92); pos += 4;
        Encoding.ASCII.GetBytes("strl").CopyTo(span[pos..]); pos += 4;

        // === strh chunk (8+56=64) ===
        Encoding.ASCII.GetBytes("strh").CopyTo(span[pos..]); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 56); pos += 4;
        Encoding.ASCII.GetBytes("auds").CopyTo(span[pos..]); pos += 4; // fccType
        // fccHandler: 4 bytes zero (PCM has no handler)
        pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;  // dwFlags
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;  // wPriority
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 0); pos += 2;  // wLanguage
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;  // dwInitialFrames
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 1); pos += 4;  // dwScale
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 44100); pos += 4; // dwRate
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;  // dwStart
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 44100); pos += 4; // dwLength
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;  // dwSuggestedBufferSize
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 0); pos += 4;  // dwQuality
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 4); pos += 4;  // dwSampleSize
        pos += 8; // rcFrame

        // === strf chunk (8+16=24) ===
        Encoding.ASCII.GetBytes("strf").CopyTo(span[pos..]); pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 16); pos += 4;
        // WAVEFORMATEX (16 bytes)
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 1); pos += 2;     // wFormatTag (PCM)
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 2); pos += 2;     // nChannels
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 44100); pos += 4; // nSamplesPerSec
        BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], 176400); pos += 4; // nAvgBytesPerSec (44100*2*2)
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 4); pos += 2;     // nBlockAlign
        BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], 16);              // wBitsPerSample

        return data;
    }

    /// <summary>
    /// OpenDML（AVI 2.0）の AVI（REQ-188）。strl に strn とスーパーインデックス（indx → ix00）、LIST 'odml' に dmlh、
    /// movi に奇数バイトの 00dc（3 バイト + 詰め物）と標準インデックス ix00、後ろに RIFF 'AVIX' の movi を置く。
    /// </summary>
    public static byte[] CreateOpenDmlAvi()
    {
        static byte[] Chunk(string id, byte[] body)
        {
            var m = new MemoryStream();
            m.Write(Encoding.ASCII.GetBytes(id));
            m.Write(BitConverter.GetBytes((uint)body.Length));
            m.Write(body);
            if (body.Length % 2 != 0) m.WriteByte(0);
            return m.ToArray();
        }
        static byte[] List(string type, params byte[][] children) =>
            Chunk("LIST", Encoding.ASCII.GetBytes(type).Concat(children.SelectMany(c => c)).ToArray());
        static byte[] Le(params uint[] values) => values.SelectMany(BitConverter.GetBytes).ToArray();

        var avih = Le(100000, 1000, 0, 0x10, 2, 0, 1, 0, 64, 48, 0, 0, 0, 0);
        var strh = Encoding.ASCII.GetBytes("vidsMJPG").Concat(Le(0, 0, 0, 1, 10, 0, 2, 0, 0xFFFFFFFF, 0)).Concat(new byte[] { 0, 0, 0, 0, 64, 0, 48, 0 }).ToArray();
        var strf = Le(40, 64, 48).Concat(BitConverter.GetBytes((ushort)1)).Concat(BitConverter.GetBytes((ushort)24))
            .Concat(Encoding.ASCII.GetBytes("MJPG")).Concat(Le(0, 0, 0, 0, 0)).ToArray();
        var strn = "video\0"u8.ToArray();

        // レイアウトを決めてから、indx の qwOffset と ix00 の qwBaseOffset を埋める
        var frame0 = new byte[] { 0xFF, 0xD8, 0xD9 };
        byte[] Indx(ulong ixOffset) => BitConverter.GetBytes((ushort)4).Concat(new byte[] { 0, 0 }).Concat(Le(1)).Concat("00dc"u8.ToArray())
            .Concat(new byte[12]).Concat(BitConverter.GetBytes(ixOffset)).Concat(Le(32, 1)).ToArray();
        byte[] Ix00(ulong baseOffset) => BitConverter.GetBytes((ushort)2).Concat(new byte[] { 0, 1 }).Concat(Le(1)).Concat("00dc"u8.ToArray())
            .Concat(BitConverter.GetBytes(baseOffset)).Concat(Le(0)).Concat(Le(0, 3)).ToArray();
        byte[] Build(ulong ixOffset, ulong baseOffset)
        {
            var hdrl = List("hdrl", Chunk("avih", avih), List("strl", Chunk("strh", strh), Chunk("strf", strf), Chunk("strn", strn), Chunk("indx", Indx(ixOffset))),
                List("odml", Chunk("dmlh", Le(2).Concat(new byte[244]).ToArray())));
            var movi = List("movi", Chunk("00dc", frame0), Chunk("ix00", Ix00(baseOffset)));
            var idx1 = Chunk("idx1", Encoding.ASCII.GetBytes("00dc").Concat(Le(0x10, 4, 3)).ToArray());
            var first = Encoding.ASCII.GetBytes("AVI ").Concat(hdrl).Concat(movi).Concat(idx1).ToArray();
            var avix = Chunk("RIFF", Encoding.ASCII.GetBytes("AVIX").Concat(List("movi", Chunk("00dc", new byte[] { 0xAB, 0xCD }))).ToArray());
            return Chunk("RIFF", first).Concat(avix).ToArray();
        }
        var draft = Build(0, 0);
        var ixPos = FindChunk(draft, "ix00");
        var framePos = FindChunk(draft, "movi") + 4 + 8;  // movi の最初の 00dc の中身
        return Build((ulong)ixPos, (ulong)framePos);
    }

    private static int FindChunk(byte[] data, string id)
    {
        var pattern = Encoding.ASCII.GetBytes(id);
        return data.AsSpan().IndexOf(pattern);
    }
}
