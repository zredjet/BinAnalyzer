using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class OggParsingTests
{
    private static readonly string OggFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "ogg.bdef.yaml");

    [Fact]
    public void OggFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(OggFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void OggFormat_MinimalVorbis_DecodesIdentificationHeader()
    {
        var pages = Pages(Decode(OggTestDataGenerator.CreateMinimalOgg()));

        pages.Should().HaveCount(2);
        ((DecodedBitfield)Child(pages[0], "header_type")).Fields.Should().Contain(f => f.Name == "is_bos" && f.Value == 1);
        ((DecodedVirtual)Child(pages[0], "packet_kind")).Value.Should().Be("vorbis");
        var packet = Child(pages[0], "page_data");
        ((DecodedInteger)Child(packet, "packet_type")).Value.Should().Be(1);
        var identification = Child(packet, "vorbis_id_header");
        ((DecodedInteger)Child(identification, "channels")).Value.Should().BeGreaterThan(0);
        ((DecodedBitfield)Child(pages[1], "header_type")).Fields.Should().Contain(f => f.Name == "is_eos" && f.Value == 1);
    }

    [Fact]
    public void OggFormat_Opus_DecodesHeadAndTags()
    {
        var pages = Pages(Decode(OggTestDataGenerator.CreateOpusOgg()));

        pages.Should().HaveCount(3);
        var head = Child(pages[0], "page_data");
        ((DecodedStruct)head).StructType.Should().Be("opus_head");
        ((DecodedInteger)Child(head, "channel_count")).Value.Should().Be(2);
        ((DecodedInteger)Child(head, "pre_skip")).Value.Should().Be(312);
        ((DecodedInteger)Child(head, "input_sample_rate")).Value.Should().Be(48000);

        var tags = Child(pages[1], "page_data");
        ((DecodedStruct)tags).StructType.Should().Be("opus_tags");
        var comment = Child(tags, "comment");
        ((DecodedString)Child(comment, "vendor_string")).Value.Should().Be("gen");
        var entry = ((DecodedArray)Child(comment, "comments")).Elements.Single();
        ((DecodedString)Child(entry, "value")).Value.Should().Be("ARTIST=Tester");
    }

    [Fact]
    public void OggFormat_PageBody_UsesSegmentTableEvenIfDataContainsCapturePattern()
    {
        var data = OggTestDataGenerator.CreateOpusOgg();
        var pages = Pages(Decode(data));

        // 3 ページ目の音声データの途中に 'OggS' があっても、ページの大きさは segment_table の合計で決まる
        var last = pages[2];
        ((DecodedVirtual)Child(last, "body_size")).Value.Should().Be(20L);
        ((DecodedVirtual)Child(last, "packet_kind")).Value.Should().Be("data");
        Child(Child(last, "page_data"), "data").Size.Should().Be(20);
        (last.Offset + last.Size).Should().Be(data.Length);
        ((DecodedInteger)Child(last, "granule_position")).Value.Should().Be(960);
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(OggFormatPath));

    private static IReadOnlyList<DecodedNode> Pages(DecodedStruct root) =>
        ((DecodedArray)Child(root, "pages")).Elements;

    private static DecodedNode Child(DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);
}
