using System.Buffers.Binary;

namespace BinAnalyzer.Integration.Tests;

public static class MidiTestDataGenerator
{
    /// <summary>
    /// 最小MIDIファイル: MThd(4B) + header_length(4B) + format(2B) + ntrks(2B) + division(2B) + MTrk(4B) + length(4B) + data(4B) = 26バイト
    /// Format 0 (single track), 120 ticks/quarter note, 1トラック（End of Track メタイベントのみ）
    /// Track data: delta_time=0x00, status=0xFF, meta_type=0x2F, meta_length=0x00
    /// </summary>
    public static byte[] CreateMinimalMidi()
    {
        var data = new byte[26];
        var span = data.AsSpan();
        var pos = 0;

        // header_magic: "MThd"
        data[0] = 0x4D; // 'M'
        data[1] = 0x54; // 'T'
        data[2] = 0x68; // 'h'
        data[3] = 0x64; // 'd'
        pos = 4;

        // header_length: 6
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 6); pos += 4;

        // format: 0 (single_track)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // ntrks: 1
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2;

        // division: 120 (ticks per quarter note, PPQN mode)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 120); pos += 2;

        // === Track: MTrk ===
        // magic: "MTrk"
        data[pos] = 0x4D; pos += 1; // 'M'
        data[pos] = 0x54; pos += 1; // 'T'
        data[pos] = 0x72; pos += 1; // 'r'
        data[pos] = 0x6B; pos += 1; // 'k'

        // length: 4 bytes of track data (End of Track meta event)
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 4); pos += 4;

        // End of Track meta event: delta_time(VLQ)=0, status=0xFF, meta_type=0x2F, meta_length(VLQ)=0
        data[pos] = 0x00; pos += 1; // delta_time
        data[pos] = 0xFF; pos += 1; // status (meta event)
        data[pos] = 0x2F; pos += 1; // meta_type (End of Track)
        data[pos] = 0x00;            // meta_length

        return data;
    }

    /// <summary>
    /// ランニングステータスと未知のチャンクを含む形式 1 の MIDI（REQ-188）。MThd の後ろに未知のチャンク 'XFIH'（3 バイト）、
    /// MTrk にトラック名・テンポ（120 BPM）・拍子（3/4）・プログラムチェンジ・ノートオン（60）・ランニングステータスのノートオン（64 と 60 のベロシティ 0）・
    /// ピッチベンド（中央）・トラックの終わり。
    /// </summary>
    public static byte[] CreateMidiWithRunningStatus()
    {
        var events = new byte[]
        {
            0x00, 0xFF, 0x03, 0x04, (byte)'T', (byte)'e', (byte)'s', (byte)'t',
            0x00, 0xFF, 0x51, 0x03, 0x07, 0xA1, 0x20,
            0x00, 0xFF, 0x58, 0x04, 0x03, 0x02, 0x18, 0x08,
            0x00, 0xC0, 0x05,
            0x00, 0x90, 0x3C, 0x64,
            0x81, 0x70, 0x40, 0x64,
            0x81, 0x70, 0x3C, 0x00,
            0x00, 0xE0, 0x00, 0x40,
            0x83, 0x60, 0xFF, 0x2F, 0x00,
        };
        var ms = new MemoryStream();
        ms.Write("MThd"u8);
        ms.Write([0, 0, 0, 6, 0, 1, 0, 1, 0x01, 0xE0]);
        ms.Write("XFIH"u8);
        ms.Write([0, 0, 0, 3, (byte)'a', (byte)'b', (byte)'c']);
        ms.Write("MTrk"u8);
        var length = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)events.Length);
        ms.Write(length);
        ms.Write(events);
        return ms.ToArray();
    }
}
