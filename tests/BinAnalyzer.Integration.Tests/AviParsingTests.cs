using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class AviParsingTests
{
    private static readonly string AviFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "avi.bdef.yaml");

    [Fact]
    public void AviFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(AviFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AviFormat_DecodesWithRecovery()
    {
        var data = AviTestDataGenerator.CreateMinimalAvi();
        var format = new YamlFormatLoader().Load(AviFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);

        // avih をわざと 52 バイト（仕様は 56 バイト）に切り詰めてあるので、dwReserved の読み取りでエラーになる
        result.Errors.Should().NotBeEmpty();
        result.Root.Name.Should().Be("AVI");
        result.Root.Children.Select(c => c.Name).Should().Equal("magic", "file_size", "form_type", "chunks");
    }

    [Fact]
    public void AviFormat_RiffHeader_DecodesCorrectly()
    {
        var decoded = DecodeWithRecovery(AviTestDataGenerator.CreateMinimalAvi());

        ((DecodedString)Child(decoded, "magic")).Value.Should().Be("RIFF");
        ((DecodedString)Child(decoded, "form_type")).Value.Should().Be("AVI ");
    }

    [Fact]
    public void AviFormat_StreamHeader_DecodesCorrectly()
    {
        var strh = Child(FindChunk(Decode(AviTestDataGenerator.CreateAviWithStreamAndIndex()), "strh"), "data");

        ((DecodedString)Child(strh, "fccType")).Value.Should().Be("vids");
        ((DecodedString)Child(strh, "fccHandler")).Value.Should().Be("H264");
    }

    [Fact]
    public void AviFormat_OldIndex_DecodesCorrectly()
    {
        var idx1 = Child(FindChunk(Decode(AviTestDataGenerator.CreateAviWithStreamAndIndex()), "idx1"), "data");

        var entries = (DecodedArray)Child(idx1, "entries");
        entries.Elements.Should().HaveCount(1);
        ((DecodedString)Child(entries.Elements[0], "dwChunkId")).Value.Should().Be("00dc");
        var flags = (DecodedFlags)Child(entries.Elements[0], "dwFlags");
        flags.RawValue.Should().Be(0x10);
        flags.FlagStates.Should().Contain(f => f.Name == "AVIIF_KEYFRAME" && f.IsSet);
    }

    [Fact]
    public void AviFormat_TreeOutput_ContainsExpectedElements()
    {
        var output = new TreeOutputFormatter().Format(DecodeWithRecovery(AviTestDataGenerator.CreateMinimalAvi()));

        output.Should().Contain("AVI");
        output.Should().Contain("magic");
        output.Should().Contain("form_type");
    }

    [Fact]
    public void AviFormat_VideoStreamFormat_DecodesBitmapInfoHeader()
    {
        var strf = Child(FindChunk(Decode(AviTestDataGenerator.CreateAviWithVideoStreamFormat()), "strf"), "data");

        var header = Child(strf, "format_data");
        ((DecodedStruct)header).StructType.Should().Be("bitmap_info_header");
        ((DecodedInteger)Child(header, "biWidth")).Value.Should().Be(320);
        ((DecodedInteger)Child(header, "biHeight")).Value.Should().Be(240);
        ((DecodedInteger)Child(header, "biBitCount")).Value.Should().Be(24);
    }

    [Fact]
    public void AviFormat_AudioStreamFormat_DecodesWaveFormatEx()
    {
        var strf = Child(FindChunk(Decode(AviTestDataGenerator.CreateAviWithAudioStreamFormat()), "strf"), "data");

        var format = Child(strf, "format_data");
        ((DecodedStruct)format).StructType.Should().Be("wave_format");
        ((DecodedInteger)Child(format, "wFormatTag")).EnumLabel.Should().Be("PCM");
        ((DecodedInteger)Child(format, "nChannels")).Value.Should().Be(2);
        ((DecodedInteger)Child(format, "nSamplesPerSec")).Value.Should().Be(44100);
    }

    [Fact]
    public void AviFormat_OpenDml_DecodesSuperIndexAndStandardIndex()
    {
        var decoded = Decode(AviTestDataGenerator.CreateOpenDmlAvi());

        var indx = Child(FindChunk(decoded, "indx"), "data");
        ((DecodedInteger)Child(indx, "bIndexType")).EnumLabel.Should().Be("AVI_INDEX_OF_INDEXES");
        ((DecodedString)Child(indx, "dwChunkId")).Value.Should().Be("00dc");
        var superEntry = ((DecodedArray)Child(Child(indx, "body"), "entries")).Elements.Single();
        var ix00 = FindChunk(decoded, "ix00");
        ((DecodedInteger)Child(superEntry, "qwOffset")).Value.Should().Be(ix00.Offset);
        ((DecodedInteger)Child(superEntry, "dwSize")).Value.Should().Be(32);

        var standard = Child(Child(ix00, "data"), "body");
        ((DecodedStruct)standard).StructType.Should().Be("standard_index_entries");
        var frame = FindChunk(decoded, "00dc");
        ((DecodedInteger)Child(standard, "qwBaseOffset")).Value.Should().Be(Child(frame, "data").Offset);
        var entry = ((DecodedArray)Child(standard, "entries")).Elements.Single();
        ((DecodedInteger)Child(entry, "dwSize")).Value.Should().Be(3);
        ((DecodedVirtual)Child(entry, "is_keyframe")).Value.Should().Be(1L);
    }

    [Fact]
    public void AviFormat_OpenDml_DecodesStreamNameAndExtendedHeader()
    {
        var decoded = Decode(AviTestDataGenerator.CreateOpenDmlAvi());

        ((DecodedString)Child(Child(FindChunk(decoded, "strn"), "data"), "name")).Value.Should().StartWith("video");
        ((DecodedInteger)Child(Child(FindChunk(decoded, "dmlh"), "data"), "dwGrandFrames")).Value.Should().Be(2);
    }

    [Fact]
    public void AviFormat_OpenDml_DecodesAvixAndOddSizedChunk()
    {
        var decoded = Decode(AviTestDataGenerator.CreateOpenDmlAvi());

        var chunks = (DecodedArray)Child(decoded, "chunks");
        chunks.Elements.Should().HaveCount(4); // hdrl, movi, idx1, RIFF 'AVIX'
        // 3 バイトの 00dc の後ろの詰め物を読み飛ばして ix00 が続く
        FindChunk(decoded, "00dc").Size.Should().Be(12);
        var avix = chunks.Elements[3];
        ((DecodedString)Child(avix, "chunk_id")).Value.Should().Be("RIFF");
        ((DecodedString)Child(Child(avix, "data"), "list_type")).Value.Should().Be("AVIX");
        var frames = FindAll(avix).Where(IsChunk("00dc")).ToList();
        frames.Should().ContainSingle();
        ((DecodedInteger)Child(frames[0], "chunk_size")).Value.Should().Be(2);
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(AviFormatPath));

    private static DecodedStruct DecodeWithRecovery(byte[] data) =>
        new BinaryDecoder().DecodeWithRecovery(data, new YamlFormatLoader().Load(AviFormatPath), ErrorMode.Continue).Root;

    private static DecodedNode Child(DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);

    /// <summary>木を深さ優先でたどり、最初に見つかった ID のチャンクを返す。</summary>
    private static DecodedNode FindChunk(DecodedNode root, string chunkId) => FindAll(root).First(IsChunk(chunkId));

    private static Func<DecodedNode, bool> IsChunk(string chunkId) => node =>
        node is DecodedStruct { StructType: "riff_chunk" } chunk
        && ((DecodedString)Child(chunk, "chunk_id")).Value == chunkId;

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
