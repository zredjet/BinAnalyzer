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
        var wavData = WavTestDataGenerator.CreateMinimalWav();
        var format = new YamlFormatLoader().Load(WavFormatPath);
        var decoded = new BinaryDecoder().Decode(wavData, format);

        decoded.Name.Should().Be("WAV");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(4);
        decoded.Children[0].Name.Should().Be("riff_id");
        decoded.Children[1].Name.Should().Be("file_size");
        decoded.Children[2].Name.Should().Be("wave_id");
        decoded.Children[3].Name.Should().Be("chunks");
    }

    [Fact]
    public void WavFormat_RiffHeader_DecodesCorrectly()
    {
        var wavData = WavTestDataGenerator.CreateMinimalWav();
        var format = new YamlFormatLoader().Load(WavFormatPath);
        var decoded = new BinaryDecoder().Decode(wavData, format);

        var riffId = decoded.Children[0].Should().BeOfType<DecodedString>().Subject;
        riffId.Value.Should().Be("RIFF");

        var fileSize = decoded.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        fileSize.Value.Should().Be(40);

        var waveId = decoded.Children[2].Should().BeOfType<DecodedString>().Subject;
        waveId.Value.Should().Be("WAVE");
    }

    [Fact]
    public void WavFormat_FmtChunk_DecodesCorrectly()
    {
        var wavData = WavTestDataGenerator.CreateMinimalWav();
        var format = new YamlFormatLoader().Load(WavFormatPath);
        var decoded = new BinaryDecoder().Decode(wavData, format);

        var chunks = decoded.Children[3].Should().BeOfType<DecodedArray>().Subject;
        var fmtChunk = chunks.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        var chunkId = fmtChunk.Children[0].Should().BeOfType<DecodedString>().Subject;
        chunkId.Value.Should().Be("fmt ");

        var chunkSize = fmtChunk.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        chunkSize.Value.Should().Be(16);

        // data は switch → fmt_chunk struct
        var fmtData = fmtChunk.Children[2].Should().BeOfType<DecodedStruct>().Subject;

        var audioFormat = fmtData.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        audioFormat.Value.Should().Be(1);
        audioFormat.EnumLabel.Should().Be("PCM");

        var numChannels = fmtData.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        numChannels.Value.Should().Be(1);

        var sampleRate = fmtData.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        sampleRate.Value.Should().Be(8000);

        var byteRate = fmtData.Children[3].Should().BeOfType<DecodedInteger>().Subject;
        byteRate.Value.Should().Be(8000);

        var blockAlign = fmtData.Children[4].Should().BeOfType<DecodedInteger>().Subject;
        blockAlign.Value.Should().Be(1);

        var bitsPerSample = fmtData.Children[5].Should().BeOfType<DecodedInteger>().Subject;
        bitsPerSample.Value.Should().Be(8);
    }

    [Fact]
    public void WavFormat_DataChunk_DecodesCorrectly()
    {
        var wavData = WavTestDataGenerator.CreateMinimalWav();
        var format = new YamlFormatLoader().Load(WavFormatPath);
        var decoded = new BinaryDecoder().Decode(wavData, format);

        var chunks = decoded.Children[3].Should().BeOfType<DecodedArray>().Subject;
        chunks.Elements.Should().HaveCount(2);

        var dataChunk = chunks.Elements[1].Should().BeOfType<DecodedStruct>().Subject;

        var chunkId = dataChunk.Children[0].Should().BeOfType<DecodedString>().Subject;
        chunkId.Value.Should().Be("data");

        var chunkSize = dataChunk.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        chunkSize.Value.Should().Be(4);
    }

    [Fact]
    public void WavFormat_TreeOutput_ContainsExpectedElements()
    {
        var wavData = WavTestDataGenerator.CreateMinimalWav();
        var format = new YamlFormatLoader().Load(WavFormatPath);
        var decoded = new BinaryDecoder().Decode(wavData, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("WAV");
        output.Should().Contain("riff_id");
        output.Should().Contain("wave_id");
        output.Should().Contain("fmt ");
        output.Should().Contain("PCM");
        output.Should().Contain("sample_rate: 8000");
        output.Should().Contain("bits_per_sample: 8");
        output.Should().Contain("data");
    }
}
