using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

/// <summary>REQ-147: Matroska / WebM（EBML）。</summary>
public class MkvParsingTests
{
    private static readonly string MkvFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "mkv.bdef.yaml");

    private static DecodedStruct Decode() =>
        new BinaryDecoder().Decode(MkvTestDataGenerator.CreateMinimalWebm(), new YamlFormatLoader().Load(MkvFormatPath));

    /// <summary>ID の名前（enum ラベル）で子要素をたどる。</summary>
    private static DecodedStruct Element(DecodedStruct parentElementOrRoot, string idLabel)
    {
        var children = parentElementOrRoot.Children.Single(c => c.Name is "elements" or "data") switch
        {
            DecodedArray top => top,
            DecodedStruct master => (DecodedArray)master.Children.Single(c => c.Name == "children"),
            var other => throw new InvalidOperationException(other.Name),
        };
        return children.Elements.Cast<DecodedStruct>()
            .Single(e => ((DecodedVirtual)e.Children.Single(c => c.Name == "id")).EnumLabel == idLabel);
    }

    private static DecodedStruct Data(DecodedStruct element) => (DecodedStruct)element.Children.Single(c => c.Name == "data");

    /// <summary>値の型（ebml_uint 等）の中の最終的な値ノード。</summary>
    private static DecodedNode Value(DecodedStruct element)
    {
        DecodedNode node = Data(element).Children.Single(c => c.Name == "value");
        while (node is DecodedStruct s)
            node = s.Children.Single(c => c.Name == "value");
        return node;
    }

    /// <summary>整数の値。1 / 2 / 4 / 8 バイトは整数フィールド、3 / 5 / 6 / 7 バイトは virtual で組み立てる。</summary>
    private static long Number(DecodedStruct element) => Value(element) switch
    {
        DecodedInteger i => i.Value,
        DecodedVirtual v => Convert.ToInt64(v.Value),
        var other => throw new InvalidOperationException(other.GetType().Name),
    };

    [Fact]
    public void MkvFormat_LoadsWithoutDiagnostics()
    {
        FormatValidator.Validate(new YamlFormatLoader().Load(MkvFormatPath)).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void EbmlHeader_DocTypeAndVersions()
    {
        var ebml = Element(Decode(), "EBML");

        ((DecodedString)Value(Element(ebml, "DocType"))).Value.Should().Be("webm");
        Number(Element(ebml, "EBMLMaxSizeLength")).Should().Be(8);
    }

    [Fact]
    public void UnknownSizeSegment_TakesTheRestOfTheFile_AndContainsInfoTracksAndCluster()
    {
        var data = MkvTestDataGenerator.CreateMinimalWebm();
        var segment = Element(Decode(), "Segment");
        ((DecodedInteger)segment.Children.Single(c => c.Name == "size_first")).Value.Should().Be(0x01, "8 バイトの VINT");
        ((DecodedVirtual)segment.Children.Single(c => c.Name == "data_size")).Value.Should().Be((1L << 56) - 1, "値ビットがすべて 1 = サイズ不定");
        var body = Data(segment);
        (body.Offset + body.Size).Should().Be(data.Length, "サイズ不定の要素はスコープの残りすべて");

        var info = Element(segment, "Info");
        Number(Element(info, "TimestampScale")).Should().Be(1_000_000, "3 バイトの整数");
        ((DecodedFloat)Value(Element(info, "Duration"))).Value.Should().Be(1000.0);
        ((DecodedString)Value(Element(info, "Title"))).Value.Should().Be("テスト");

        var track = Element(Element(segment, "Tracks"), "TrackEntry");
        ((DecodedString)Value(Element(track, "CodecID"))).Value.Should().Be("V_VP8");
        ((DecodedInteger)Value(Element(track, "TrackType"))).EnumLabel.Should().Be("video");
        Number(Element(track, "TrackUID")).Should().Be(0x123456789A, "5 バイトの整数");
        Number(Element(Element(track, "Video"), "PixelWidth")).Should().Be(16);
    }

    [Fact]
    public void Cluster_SimpleBlockHeaderIsDecoded()
    {
        var cluster = Element(Element(Decode(), "Segment"), "Cluster");

        var block = Data(Element(cluster, "SimpleBlock"));
        ((DecodedVirtual)block.Children.Single(c => c.Name == "track_number")).Value.Should().Be(1L);
        var flags = (DecodedBitfield)block.Children.Single(c => c.Name == "flags");
        flags.Fields.Single(f => f.Name == "keyframe").Value.Should().Be(1);
        ((DecodedBytes)block.Children.Single(c => c.Name == "frame_data")).RawBytes.ToArray().Should().Equal("frame"u8.ToArray());
    }
}
