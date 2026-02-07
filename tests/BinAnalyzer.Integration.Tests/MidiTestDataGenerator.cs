using System.Buffers.Binary;

namespace BinAnalyzer.Integration.Tests;

public static class MidiTestDataGenerator
{
    /// <summary>
    /// 最小MIDIファイル: MThd(4B) + header_length(4B) + format(2B) + ntrks(2B) + division(2B) + MTrk(4B) + length(4B) + data(1B) = 23バイト
    /// Format 0 (single track), 120 ticks/quarter note, 1トラック（End of Track メタイベントのみ）
    /// </summary>
    public static byte[] CreateMinimalMidi()
    {
        var data = new byte[23];
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

        // length: 1 byte of track data
        BinaryPrimitives.WriteUInt32BigEndian(span[pos..], 1); pos += 4;

        // data: 1 byte (dummy - normally End of Track meta event)
        data[pos] = 0x00;

        return data;
    }
}
