using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class MidiParsingTests
{
    private static readonly string MidiFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "midi.bdef.yaml");

    [Fact]
    public void MidiFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(MidiFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void MidiFormat_DecodesMinimalMidi()
    {
        var decoded = Decode(MidiTestDataGenerator.CreateMinimalMidi());

        decoded.Name.Should().Be("MIDI");
        decoded.Children.Select(c => c.Name).Should().StartWith(["header_magic", "header_length", "format", "ntrks", "division"]);
        decoded.Children[^1].Name.Should().Be("tracks");
    }

    [Fact]
    public void MidiFormat_Header_DecodesCorrectly()
    {
        var decoded = Decode(MidiTestDataGenerator.CreateMinimalMidi());

        ((DecodedBytes)Child(decoded, "header_magic")).ValidationPassed.Should().BeTrue();
        ((DecodedInteger)Child(decoded, "header_length")).Value.Should().Be(6);
        var format = (DecodedInteger)Child(decoded, "format");
        format.Value.Should().Be(0);
        format.EnumLabel.Should().Be("single_track");
        ((DecodedInteger)Child(decoded, "ntrks")).Value.Should().Be(1);
        ((DecodedVirtual)Child(decoded, "ticks_per_quarter_note")).Value.Should().Be(120L);
    }

    [Fact]
    public void MidiFormat_Track_DecodesCorrectly()
    {
        var decoded = Decode(MidiTestDataGenerator.CreateMinimalMidi());

        var tracks = (DecodedArray)Child(decoded, "tracks");
        tracks.Elements.Should().HaveCount(1);
        var track = tracks.Elements[0];
        ((DecodedString)Child(track, "magic")).Value.Should().Be("MTrk");
        ((DecodedInteger)Child(track, "length")).Value.Should().Be(4);
        ((DecodedStruct)Child(track, "body")).StructType.Should().Be("track_events");
    }

    [Fact]
    public void MidiFormat_EndOfTrackEvent_DecodesCorrectly()
    {
        var events = Events(Decode(MidiTestDataGenerator.CreateMinimalMidi()), 0);

        events.Should().HaveCount(1);
        var evt = events[0];
        ((DecodedInteger)Child(evt, "delta_time")).Value.Should().Be(0);
        ((DecodedVirtual)Child(evt, "status")).Value.Should().Be(0xFFL);
        var meta = Child(evt, "event_data");
        var metaType = (DecodedInteger)Child(meta, "meta_type");
        metaType.Value.Should().Be(0x2F);
        metaType.EnumLabel.Should().Be("end_of_track");
        ((DecodedInteger)Child(meta, "meta_length")).Value.Should().Be(0);
    }

    [Fact]
    public void MidiFormat_UnknownChunk_IsSkipped()
    {
        var tracks = ((DecodedArray)Child(Decode(MidiTestDataGenerator.CreateMidiWithRunningStatus()), "tracks")).Elements;

        tracks.Should().HaveCount(2);
        ((DecodedString)Child(tracks[0], "magic")).Value.Should().Be("XFIH");
        Child(Child(tracks[0], "body"), "data").Size.Should().Be(3);
        ((DecodedString)Child(tracks[1], "magic")).Value.Should().Be("MTrk");
        ((DecodedArray)Child(Child(tracks[1], "body"), "events")).Elements.Should().HaveCount(9);
    }

    [Fact]
    public void MidiFormat_RunningStatus_ReusesPreviousStatus()
    {
        var events = Events(Decode(MidiTestDataGenerator.CreateMidiWithRunningStatus()), 1);

        var noteOn = events[4];
        ((DecodedVirtual)Child(noteOn, "is_running_status")).Value.Should().Be(0L);
        ((DecodedVirtual)Child(noteOn, "event_type")).EnumLabel.Should().Be("note_on");
        ((DecodedVirtual)Child(Child(noteOn, "event_data"), "note")).Value.Should().Be(60L);

        // 状態バイトを省いたイベントは 1 バイト目がノート番号
        var running = events[5];
        ((DecodedVirtual)Child(running, "is_running_status")).Value.Should().Be(1L);
        ((DecodedVirtual)Child(running, "status")).Value.Should().Be(0x90L);
        ((DecodedInteger)Child(running, "delta_time")).Value.Should().Be(240);
        var runningNote = Child(running, "event_data");
        ((DecodedVirtual)Child(runningNote, "note")).Value.Should().Be(64L);
        ((DecodedInteger)Child(runningNote, "velocity")).Value.Should().Be(100);

        var noteOff = Child(events[6], "event_data");
        ((DecodedVirtual)Child(noteOff, "note")).Value.Should().Be(60L);
        ((DecodedInteger)Child(noteOff, "velocity")).Value.Should().Be(0);

        // 新しい状態バイトで切り替わる
        var bend = events[7];
        ((DecodedVirtual)Child(bend, "is_running_status")).Value.Should().Be(0L);
        ((DecodedVirtual)Child(bend, "event_type")).EnumLabel.Should().Be("pitch_bend");
        ((DecodedVirtual)Child(Child(bend, "event_data"), "bend")).Value.Should().Be(0L);
    }

    [Fact]
    public void MidiFormat_MetaEvents_DecodeTempoAndTimeSignature()
    {
        var events = Events(Decode(MidiTestDataGenerator.CreateMidiWithRunningStatus()), 1);

        var name = Child(events[0], "event_data");
        ((DecodedInteger)Child(name, "meta_type")).EnumLabel.Should().Be("track_name");
        ((DecodedString)Child(Child(name, "meta_data"), "text")).Value.Should().Be("Test");

        var tempo = Child(Child(events[1], "event_data"), "meta_data");
        ((DecodedBitfield)Child(tempo, "tempo")).Fields.Should().Contain(f => f.Name == "microseconds_per_quarter" && f.Value == 500000);
        ((DecodedVirtual)Child(tempo, "bpm")).Value.Should().Be(120L);

        var timeSignature = Child(Child(events[2], "event_data"), "meta_data");
        ((DecodedInteger)Child(timeSignature, "numerator")).Value.Should().Be(3);
        ((DecodedVirtual)Child(timeSignature, "denominator")).Value.Should().Be(4L);

        var program = events[3];
        ((DecodedVirtual)Child(program, "event_type")).EnumLabel.Should().Be("program_change");
        ((DecodedVirtual)Child(Child(program, "event_data"), "param")).Value.Should().Be(5L);
    }

    [Fact]
    public void MidiFormat_TreeOutput_ContainsExpectedElements()
    {
        var output = new TreeOutputFormatter().Format(Decode(MidiTestDataGenerator.CreateMinimalMidi()));

        output.Should().Contain("MIDI");
        output.Should().Contain("header_magic");
        output.Should().Contain("single_track");
        output.Should().Contain("ntrks");
        output.Should().Contain("division");
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(MidiFormatPath));

    private static IReadOnlyList<DecodedNode> Events(DecodedStruct root, int track) =>
        ((DecodedArray)Child(Child(((DecodedArray)Child(root, "tracks")).Elements[track], "body"), "events")).Elements;

    private static DecodedNode Child(DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);
}
