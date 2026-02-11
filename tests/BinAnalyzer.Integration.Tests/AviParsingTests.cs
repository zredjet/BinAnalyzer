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
        var decoded = result.Root;

        decoded.Name.Should().Be("AVI");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(3);
        decoded.Children[0].Name.Should().Be("magic");
        decoded.Children[1].Name.Should().Be("file_size");
        decoded.Children[2].Name.Should().Be("form_type");
    }

    [Fact]
    public void AviFormat_RiffHeader_DecodesCorrectly()
    {
        var data = AviTestDataGenerator.CreateMinimalAvi();
        var format = new YamlFormatLoader().Load(AviFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var magic = decoded.Children[0].Should().BeOfType<DecodedString>().Subject;
        magic.Value.Should().Be("RIFF");

        var formType = decoded.Children[2].Should().BeOfType<DecodedString>().Subject;
        formType.Value.Should().Be("AVI ");
    }

    [Fact]
    public void AviFormat_StreamHeader_DecodesCorrectly()
    {
        var data = AviTestDataGenerator.CreateAviWithStreamAndIndex();
        var format = new YamlFormatLoader().Load(AviFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var chunks = decoded.Children[3].Should().BeOfType<DecodedArray>().Subject;
        // chunks[0] = LIST(hdrl), chunks[1] = idx1
        var hdrlList = chunks.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var listData = hdrlList.Children[2].Should().BeOfType<DecodedStruct>().Subject; // list_chunk

        var subChunks = listData.Children[1].Should().BeOfType<DecodedArray>().Subject;
        // subChunks[0] = avih, subChunks[1] = LIST(strl)
        var strlChunk = subChunks.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        var strlData = strlChunk.Children[2].Should().BeOfType<DecodedStruct>().Subject; // list_chunk
        var strlSubChunks = strlData.Children[1].Should().BeOfType<DecodedArray>().Subject;

        var strhChunk = strlSubChunks.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var strhData = strhChunk.Children[2].Should().BeOfType<DecodedStruct>().Subject; // stream_header

        var fccType = strhData.Children[0].Should().BeOfType<DecodedString>().Subject;
        fccType.Value.Should().Be("vids");

        var fccHandler = strhData.Children[1].Should().BeOfType<DecodedString>().Subject;
        fccHandler.Value.Should().Be("H264");
    }

    [Fact]
    public void AviFormat_OldIndex_DecodesCorrectly()
    {
        var data = AviTestDataGenerator.CreateAviWithStreamAndIndex();
        var format = new YamlFormatLoader().Load(AviFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var chunks = decoded.Children[3].Should().BeOfType<DecodedArray>().Subject;
        // chunks[1] = idx1
        var idx1Chunk = chunks.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        var idx1Data = idx1Chunk.Children[2].Should().BeOfType<DecodedStruct>().Subject; // avi_old_index

        var entries = idx1Data.Children[0].Should().BeOfType<DecodedArray>().Subject;
        entries.Elements.Should().HaveCount(1);

        var entry = entries.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var chunkId = entry.Children[0].Should().BeOfType<DecodedString>().Subject;
        chunkId.Value.Should().Be("00dc");

        var flags = entry.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        flags.Value.Should().Be(0x10);
    }

    [Fact]
    public void AviFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = AviTestDataGenerator.CreateMinimalAvi();
        var format = new YamlFormatLoader().Load(AviFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var output = new TreeOutputFormatter().Format(result.Root);

        output.Should().Contain("AVI");
        output.Should().Contain("magic");
        output.Should().Contain("form_type");
    }
}
