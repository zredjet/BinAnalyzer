using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class WavParsingTests
{
    private static readonly string WavFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "wav.bdef.yaml");

    [Fact]
    public void WavFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(WavFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void WavFormat_DecodesMinimalWav()
    {
        var decoded = Decode(WavTestDataGenerator.CreateMinimalWav());

        decoded.Name.Should().Be("WAV");
        decoded.Children.Select(c => c.Name).Should().Equal("riff_id", "file_size", "wave_id", "chunks");
    }

    [Fact]
    public void WavFormat_RiffHeader_DecodesCorrectly()
    {
        var decoded = Decode(WavTestDataGenerator.CreateMinimalWav());

        var riffId = (DecodedString)Child(decoded, "riff_id");
        riffId.Value.Should().Be("RIFF");
        riffId.Validation!.Passed.Should().BeTrue();
        ((DecodedInteger)Child(decoded, "file_size")).Value.Should().Be(40);
        ((DecodedString)Child(decoded, "wave_id")).Value.Should().Be("WAVE");
    }

    [Fact]
    public void WavFormat_FmtChunk_DecodesCorrectly()
    {
        var fmtChunk = Chunk(Decode(WavTestDataGenerator.CreateMinimalWav()), 0);

        ((DecodedString)Child(fmtChunk, "chunk_id")).Value.Should().Be("fmt ");
        ((DecodedInteger)Child(fmtChunk, "chunk_size")).Value.Should().Be(16);
        var fmt = (DecodedStruct)Child(fmtChunk, "data");
        fmt.StructType.Should().Be("wave_format");
        var formatTag = (DecodedInteger)Child(fmt, "wFormatTag");
        formatTag.Value.Should().Be(1);
        formatTag.EnumLabel.Should().Be("PCM");
        ((DecodedInteger)Child(fmt, "nChannels")).Value.Should().Be(1);
        ((DecodedInteger)Child(fmt, "nSamplesPerSec")).Value.Should().Be(8000);
        ((DecodedInteger)Child(fmt, "nAvgBytesPerSec")).Value.Should().Be(8000);
        ((DecodedInteger)Child(fmt, "nBlockAlign")).Value.Should().Be(1);
        ((DecodedInteger)Child(fmt, "wBitsPerSample")).Value.Should().Be(8);
        // 16 バイトの fmt には cbSize が無い
        fmt.Children.Should().NotContain(c => c.Name == "cbSize");
    }

    [Fact]
    public void WavFormat_DataChunk_DecodesCorrectly()
    {
        var decoded = Decode(WavTestDataGenerator.CreateMinimalWav());

        ((DecodedArray)Child(decoded, "chunks")).Elements.Should().HaveCount(2);
        var dataChunk = Chunk(decoded, 1);
        ((DecodedString)Child(dataChunk, "chunk_id")).Value.Should().Be("data");
        ((DecodedInteger)Child(dataChunk, "chunk_size")).Value.Should().Be(4);
        Child(Child(dataChunk, "data"), "data").Size.Should().Be(4);
    }

    [Fact]
    public void WavFormat_ListInfo_DecodesCorrectly()
    {
        var decoded = Decode(WavTestDataGenerator.CreateWavWithListInfo());

        ((DecodedArray)Child(decoded, "chunks")).Elements.Should().HaveCount(2); // fmt + LIST
        var listChunk = Chunk(decoded, 1);
        ((DecodedString)Child(listChunk, "chunk_id")).Value.Should().Be("LIST");
        var list = Child(listChunk, "data");
        ((DecodedString)Child(list, "list_type")).Value.Should().Be("INFO");
        var subChunks = (DecodedArray)Child(Child(list, "data"), "sub_chunks");
        subChunks.Elements.Should().HaveCount(2);

        ((DecodedString)Child(subChunks.Elements[0], "chunk_id")).Value.Should().Be("INAM");
        ((DecodedInteger)Child(subChunks.Elements[0], "chunk_size")).Value.Should().Be(5);
        ((DecodedString)Child(subChunks.Elements[1], "chunk_id")).Value.Should().Be("ISFT");
        ((DecodedInteger)Child(subChunks.Elements[1], "chunk_size")).Value.Should().Be(4);
    }

    [Fact]
    public void WavFormat_PcmWith18ByteFmt_ReadsCbSize()
    {
        var decoded = Decode(WavTestDataGenerator.CreatePcmWavWith18ByteFmt());

        var fmt = Child(Chunk(decoded, 0), "data");
        ((DecodedInteger)Child(fmt, "wFormatTag")).EnumLabel.Should().Be("PCM");
        ((DecodedInteger)Child(fmt, "cbSize")).Value.Should().Be(0);
        // cbSize を読んだ後も data チャンクの位置がずれない
        var dataChunk = Chunk(decoded, 1);
        ((DecodedString)Child(dataChunk, "chunk_id")).Value.Should().Be("data");
        ((DecodedInteger)Child(dataChunk, "chunk_size")).Value.Should().Be(4);
    }

    [Fact]
    public void WavFormat_Rf64_UsesDs64Sizes()
    {
        var decoded = Decode(WavTestDataGenerator.CreateRf64ExtensibleWav());

        ((DecodedString)Child(decoded, "riff_id")).Value.Should().Be("RF64");
        ((DecodedArray)Child(decoded, "chunks")).Elements.Should().HaveCount(4);

        var ds64 = Child(Chunk(decoded, 0), "data");
        ((DecodedInteger)Child(ds64, "riff_size")).Value.Should().Be(120);
        ((DecodedInteger)Child(ds64, "ds64_data_size")).Value.Should().Be(12);
        ((DecodedInteger)Child(ds64, "sample_count")).Value.Should().Be(2);

        // data チャンクの 0xFFFFFFFF は ds64 の大きさに置き換える
        var dataChunk = Chunk(decoded, 3);
        ((DecodedInteger)Child(dataChunk, "chunk_size")).Value.Should().Be(0xFFFFFFFF);
        ((DecodedVirtual)Child(dataChunk, "body_size")).Value.Should().Be(12L);
        Child(Child(dataChunk, "data"), "data").Size.Should().Be(12);
    }

    [Fact]
    public void WavFormat_Extensible_DecodesSubFormatAndChannelMask()
    {
        var fmt = Child(Chunk(Decode(WavTestDataGenerator.CreateRf64ExtensibleWav()), 1), "data");

        ((DecodedInteger)Child(fmt, "wFormatTag")).EnumLabel.Should().Be("EXTENSIBLE");
        ((DecodedInteger)Child(fmt, "cbSize")).Value.Should().Be(22);
        var extensible = Child(fmt, "extensible");
        ((DecodedInteger)Child(extensible, "wValidBitsPerSample")).Value.Should().Be(24);
        var mask = (DecodedFlags)Child(extensible, "dwChannelMask");
        mask.FlagStates.Where(f => f.IsSet).Select(f => f.Name).Should().Equal("FRONT_LEFT", "FRONT_RIGHT");
        ((DecodedInteger)Child(extensible, "sub_format_tag")).EnumLabel.Should().Be("PCM");
    }

    [Fact]
    public void WavFormat_OddSizedChunk_SkipsPaddingByte()
    {
        var decoded = Decode(WavTestDataGenerator.CreateRf64ExtensibleWav());

        var note = Chunk(decoded, 2);
        ((DecodedString)Child(note, "chunk_id")).Value.Should().Be("note");
        ((DecodedInteger)Child(note, "chunk_size")).Value.Should().Be(3);
        note.Size.Should().Be(12); // ヘッダ 8 + 中身 3 + 詰め物 1
        ((DecodedString)Child(Chunk(decoded, 3), "chunk_id")).Value.Should().Be("data");
    }

    [Fact]
    public void WavFormat_TreeOutput_ContainsExpectedElements()
    {
        var output = new TreeOutputFormatter().Format(Decode(WavTestDataGenerator.CreateMinimalWav()));

        output.Should().Contain("WAV");
        output.Should().Contain("riff_id");
        output.Should().Contain("wave_id");
        output.Should().Contain("fmt ");
        output.Should().Contain("PCM");
        output.Should().Contain("nSamplesPerSec: 8000");
        output.Should().Contain("wBitsPerSample: 8");
        output.Should().Contain("data");
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(WavFormatPath));

    private static DecodedNode Chunk(DecodedStruct root, int index) =>
        ((DecodedArray)Child(root, "chunks")).Elements[index];

    private static DecodedNode Child(DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);
}
