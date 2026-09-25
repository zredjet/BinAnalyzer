using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class Mp4ParsingTests
{
    private static readonly string Mp4FormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "mp4.bdef.yaml");

    [Fact]
    public void Mp4Format_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(Mp4FormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Mp4Format_DecodesSuccessfully()
    {
        var mp4Data = Mp4TestDataGenerator.CreateMinimalMp4();
        var format = new YamlFormatLoader().Load(Mp4FormatPath);
        var decoded = new BinaryDecoder().Decode(mp4Data, format);

        decoded.Name.Should().Be("MP4");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(1);
        decoded.Children[0].Name.Should().Be("boxes");
    }

    [Fact]
    public void Mp4Format_FtypBox_DecodesCorrectly()
    {
        var mp4Data = Mp4TestDataGenerator.CreateMinimalMp4();
        var format = new YamlFormatLoader().Load(Mp4FormatPath);
        var decoded = new BinaryDecoder().Decode(mp4Data, format);

        var boxes = decoded.Children[0].Should().BeOfType<DecodedArray>().Subject;
        boxes.Elements.Should().HaveCountGreaterThanOrEqualTo(2);

        // boxes[0] = ftyp iso_box
        var ftypBox = boxes.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        var boxType = ftypBox.Children.First(c => c.Name == "box_type")
            .Should().BeOfType<DecodedString>().Subject;
        boxType.Value.Should().Be("ftyp");

        // box_data → ftyp_box
        var boxData = ftypBox.Children.First(c => c.Name == "box_data")
            .Should().BeOfType<DecodedStruct>().Subject;

        var majorBrand = boxData.Children.First(c => c.Name == "major_brand")
            .Should().BeOfType<DecodedString>().Subject;
        majorBrand.Value.Should().Be("isom");

        var minorVersion = boxData.Children.First(c => c.Name == "minor_version")
            .Should().BeOfType<DecodedInteger>().Subject;
        minorVersion.Value.Should().Be(0x200);
    }

    [Fact]
    public void Mp4Format_ContainerBoxes_Decoded()
    {
        var mp4Data = Mp4TestDataGenerator.CreateMinimalMp4();
        var format = new YamlFormatLoader().Load(Mp4FormatPath);
        var decoded = new BinaryDecoder().Decode(mp4Data, format);

        var boxes = decoded.Children[0].Should().BeOfType<DecodedArray>().Subject;

        // boxes[1] = moov iso_box
        var moovBox = boxes.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        var moovType = moovBox.Children.First(c => c.Name == "box_type")
            .Should().BeOfType<DecodedString>().Subject;
        moovType.Value.Should().Be("moov");

        // moov → container_box → children
        var moovData = moovBox.Children.First(c => c.Name == "box_data")
            .Should().BeOfType<DecodedStruct>().Subject;
        var moovChildren = moovData.Children[0].Should().BeOfType<DecodedArray>().Subject;
        moovChildren.Elements.Should().HaveCountGreaterThanOrEqualTo(2); // mvhd + trak

        // children[1] = trak
        var trakBox = moovChildren.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        var trakType = trakBox.Children.First(c => c.Name == "box_type")
            .Should().BeOfType<DecodedString>().Subject;
        trakType.Value.Should().Be("trak");
    }

    [Fact]
    public void Mp4Format_TreeOutput_ContainsExpectedElements()
    {
        var mp4Data = Mp4TestDataGenerator.CreateMinimalMp4();
        var format = new YamlFormatLoader().Load(Mp4FormatPath);
        var decoded = new BinaryDecoder().Decode(mp4Data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("MP4");
        output.Should().Contain("ftyp");
        output.Should().Contain("isom");
        output.Should().Contain("moov");
        output.Should().Contain("trak");
        output.Should().Contain("soun");
    }

    [Fact]
    public void Mp4Format_SampleEntries_DecodeAvcCAndEsds()
    {
        var decoded = Decode(Mp4TestDataGenerator.CreateFragmentedMp4());

        var stsdEntries = FindAll(decoded).Where(n => n is DecodedStruct { StructType: "sample_entry" }).ToList();
        stsdEntries.Select(e => ((DecodedString)Child(e, "format")).Value).Should().Equal("avc1", "mp4a");

        var visual = Child(stsdEntries[0], "body");
        ((DecodedInteger)Child(visual, "width")).Value.Should().Be(64);
        ((DecodedInteger)Child(visual, "height")).Value.Should().Be(48);
        var avcc = Child(FindBox(visual, "avcC"), "box_data");
        ((DecodedInteger)Child(avcc, "avc_profile_indication")).Value.Should().Be(0x64);
        ((DecodedArray)Child(avcc, "sps")).Elements.Should().HaveCount(1);
        ((DecodedInteger)Child(avcc, "num_of_pps")).Value.Should().Be(1);

        var audio = Child(stsdEntries[1], "body");
        ((DecodedInteger)Child(audio, "channelcount")).Value.Should().Be(2);
        ((DecodedVirtual)Child(audio, "samplerate_hz")).Value.Should().Be(44100L);
        var decoderConfig = FindAll(audio).First(n => n is DecodedStruct { StructType: "decoder_config_descriptor" });
        ((DecodedInteger)Child(decoderConfig, "object_type_indication")).EnumLabel.Should().Be("MPEG4_Audio");
        ((DecodedBitfield)Child(decoderConfig, "stream_info")).Fields.Should().Contain(f => f.Name == "stream_type" && f.EnumLabel == "AudioStream");
        var asc = (DecodedBitfield)FindAll(decoderConfig).First(n => n.Name == "audio_specific_config");
        asc.Fields.Should().Contain(f => f.Name == "audio_object_type" && f.EnumLabel == "AAC_LC");
        asc.Fields.Should().Contain(f => f.Name == "sampling_frequency_index" && f.Value == 4); // 44100 Hz
        asc.Fields.Should().Contain(f => f.Name == "channel_configuration" && f.Value == 2);
    }

    [Fact]
    public void Mp4Format_Ilst_DecodesTextMetadata()
    {
        var decoded = Decode(Mp4TestDataGenerator.CreateFragmentedMp4());

        var item = FindAll(decoded).First(n => n is DecodedStruct { StructType: "ilst_item" });
        ((DecodedString)Child(item, "item_type")).Value.Should().Be("©nam");
        var data = Child(FindBox(item, "data"), "box_data");
        ((DecodedInteger)Child(data, "data_type")).Value.Should().Be(1); // UTF-8
        ((DecodedString)Child(Child(data, "value"), "text")).Value.Should().Be("Title");
    }

    [Fact]
    public void Mp4Format_Fragments_DecodeMoofAndTrun()
    {
        var decoded = Decode(Mp4TestDataGenerator.CreateFragmentedMp4());

        var moof = FindBox(decoded, "moof");
        ((DecodedInteger)Child(Child(FindBox(moof, "mfhd"), "box_data"), "sequence_number")).Value.Should().Be(1);

        var tfhd = Child(FindBox(moof, "tfhd"), "box_data");
        ((DecodedInteger)Child(tfhd, "track_id")).Value.Should().Be(1);
        ((DecodedInteger)Child(tfhd, "default_sample_duration")).Value.Should().Be(1);
        ((DecodedStruct)tfhd).Children.Should().NotContain(c => c.Name == "base_data_offset");

        var tfdt = Child(FindBox(moof, "tfdt"), "box_data");
        ((DecodedInteger)Child(tfdt, "version")).Value.Should().Be(1);
        ((DecodedInteger)Child(tfdt, "base_media_decode_time_v1")).Value.Should().Be(0);

        var trun = Child(FindBox(moof, "trun"), "box_data");
        ((DecodedInteger)Child(trun, "sample_count")).Value.Should().Be(2);
        // data_offset は moof の先頭から mdat の中身までの距離
        var mdat = FindBox(decoded, "mdat");
        ((DecodedInteger)Child(trun, "data_offset")).Value.Should().Be(Child(mdat, "box_data").Offset - moof.Offset);
        var samples = ((DecodedArray)Child(trun, "samples")).Elements;
        samples.Should().HaveCount(2);
        samples.Select(x => ((DecodedInteger)Child(x, "sample_size")).Value).Should().Equal(5, 5);
    }

    [Fact]
    public void Mp4Format_Mvex_DecodesTrackExtends()
    {
        var mvex = FindBox(Decode(Mp4TestDataGenerator.CreateFragmentedMp4()), "mvex");

        var trex = FindAll(mvex).Where(n => n is DecodedStruct { StructType: "trex_box" }).ToList();
        trex.Should().HaveCount(2);
        trex.Select(t => ((DecodedInteger)Child(t, "track_id")).Value).Should().Equal(1, 2);
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(Mp4FormatPath));

    private static DecodedNode Child(DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);

    /// <summary>木を深さ優先でたどり、最初に見つかった種類のボックスを返す。</summary>
    private static DecodedNode FindBox(DecodedNode root, string boxType) =>
        FindAll(root).First(n => n is DecodedStruct { StructType: "iso_box" } box
            && ((DecodedString)Child(box, "box_type")).Value == boxType);

    private static IEnumerable<DecodedNode> FindAll(DecodedNode node)
    {
        yield return node;
        var children = node switch
        {
            DecodedStruct s => s.Children,
            DecodedArray a => a.Elements,
            _ => [],
        };
        foreach (var descendant in children.SelectMany(FindAll))
            yield return descendant;
    }
}
