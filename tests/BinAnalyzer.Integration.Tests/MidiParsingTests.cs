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
        var data = MidiTestDataGenerator.CreateMinimalMidi();
        var format = new YamlFormatLoader().Load(MidiFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("MIDI");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(6);
        decoded.Children[0].Name.Should().Be("header_magic");
        decoded.Children[1].Name.Should().Be("header_length");
        decoded.Children[2].Name.Should().Be("format");
        decoded.Children[3].Name.Should().Be("ntrks");
        decoded.Children[4].Name.Should().Be("division");
    }

    [Fact]
    public void MidiFormat_Header_DecodesCorrectly()
    {
        var data = MidiTestDataGenerator.CreateMinimalMidi();
        var format = new YamlFormatLoader().Load(MidiFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var magic = decoded.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        magic.ValidationPassed.Should().BeTrue();

        var headerLength = decoded.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        headerLength.Value.Should().Be(6);

        var format_ = decoded.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        format_.Value.Should().Be(0);
        format_.EnumLabel.Should().Be("single_track");

        var ntrks = decoded.Children[3].Should().BeOfType<DecodedInteger>().Subject;
        ntrks.Value.Should().Be(1);
    }

    [Fact]
    public void MidiFormat_Track_DecodesCorrectly()
    {
        var data = MidiTestDataGenerator.CreateMinimalMidi();
        var format = new YamlFormatLoader().Load(MidiFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var tracks = decoded.Children.Last().Should().BeOfType<DecodedArray>().Subject;
        tracks.Elements.Should().HaveCount(1);

        var track = tracks.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var trackMagic = track.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        trackMagic.ValidationPassed.Should().BeTrue();

        var trackLength = track.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        trackLength.Value.Should().Be(4);

        track.Children[2].Name.Should().Be("track_data");
    }

    [Fact]
    public void MidiFormat_EndOfTrackEvent_DecodesCorrectly()
    {
        var data = MidiTestDataGenerator.CreateMinimalMidi();
        var format = new YamlFormatLoader().Load(MidiFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var tracks = decoded.Children.Last().Should().BeOfType<DecodedArray>().Subject;
        var track = tracks.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        // mtrk: magic(0), length(1), track_data(2, switch → midi_event_list)
        var trackData = track.Children[2].Should().BeOfType<DecodedStruct>().Subject;

        // midi_event_list: events (array)
        var events = trackData.Children[0].Should().BeOfType<DecodedArray>().Subject;
        events.Elements.Should().HaveCount(1);

        var evt = events.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        // midi_event: delta_time(0), status(1), event_data(2)
        var deltaTime = evt.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        deltaTime.Name.Should().Be("delta_time");
        deltaTime.Value.Should().Be(0);

        var status = evt.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        status.Name.Should().Be("status");
        status.Value.Should().Be(0xFF);

        // event_data → system_event_data → system_body → meta_event_body
        var eventData = evt.Children[2].Should().BeOfType<DecodedStruct>().Subject;
        var systemBody = eventData.Children[0].Should().BeOfType<DecodedStruct>().Subject;

        // meta_event_body: meta_type(0), meta_length(1), meta_data(2)
        var metaType = systemBody.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        metaType.Name.Should().Be("meta_type");
        metaType.Value.Should().Be(0x2F);

        var metaLength = systemBody.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        metaLength.Name.Should().Be("meta_length");
        metaLength.Value.Should().Be(0);
    }

    [Fact]
    public void MidiFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = MidiTestDataGenerator.CreateMinimalMidi();
        var format = new YamlFormatLoader().Load(MidiFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("MIDI");
        output.Should().Contain("header_magic");
        output.Should().Contain("single_track");
        output.Should().Contain("ntrks");
        output.Should().Contain("division");
    }
}
