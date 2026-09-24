using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class JpegParsingTests
{
    private static readonly string JpegFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "jpeg.bdef.yaml");

    [Fact]
    public void JpegFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(JpegFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void JpegFormat_DecodesMinimalJpeg()
    {
        var data = JpegTestDataGenerator.CreateMinimalJpeg();
        var format = new YamlFormatLoader().Load(JpegFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("JPEG");
        decoded.Children.Should().HaveCount(2);
        decoded.Children[0].Name.Should().Be("soi");
        decoded.Children[1].Name.Should().Be("segments");
    }

    [Fact]
    public void JpegFormat_Soi_DecodesCorrectly()
    {
        var data = JpegTestDataGenerator.CreateMinimalJpeg();
        var format = new YamlFormatLoader().Load(JpegFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var soi = decoded.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        soi.ValidationPassed.Should().BeTrue();
    }

    [Fact]
    public void JpegFormat_App0Segment_DecodesCorrectly()
    {
        var data = JpegTestDataGenerator.CreateMinimalJpeg();
        var format = new YamlFormatLoader().Load(JpegFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var segments = decoded.Children[1].Should().BeOfType<DecodedArray>().Subject;
        segments.Elements.Should().HaveCountGreaterThanOrEqualTo(1);

        var app0Segment = segments.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var markerType = app0Segment.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        markerType.Value.Should().Be(0xE0);
        markerType.EnumLabel.Should().Be("APP0");
    }

    [Fact]
    public void JpegFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = JpegTestDataGenerator.CreateMinimalJpeg();
        var format = new YamlFormatLoader().Load(JpegFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("JPEG");
        output.Should().Contain("soi");
        output.Should().Contain("segments");
        output.Should().Contain("APP0");
    }

    [Fact]
    public void JpegFormat_WithEntropyData_SeparatesCompressedDataAndEoi()
    {
        var data = JpegTestDataGenerator.CreateJpegWithEntropyData();
        var format = new YamlFormatLoader().Load(JpegFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var segments = decoded.Children[1].Should().BeOfType<DecodedArray>().Subject;

        // Find the SOS segment (marker_type == 0xDA)
        var sosSegment = segments.Elements
            .Cast<DecodedStruct>()
            .First(s =>
            {
                var markerType = s.Children[1] as DecodedInteger;
                return markerType?.Value == 0xDA;
            });

        // body → sos_segment struct
        var sosBody = sosSegment.Children[2].Should().BeOfType<DecodedStruct>().Subject;

        // compressed_data should be 5 bytes (entropy data only, not including EOI)
        var compressedData = sosBody.Children
            .OfType<DecodedBytes>()
            .First(b => b.Name == "compressed_data");
        compressedData.RawBytes.Length.Should().Be(5);

        // EOI marker should be a separate segment after SOS
        var eoiSegment = segments.Elements
            .Cast<DecodedStruct>()
            .First(s =>
            {
                var markerType = s.Children[1] as DecodedInteger;
                return markerType?.Value == 0xD9;
            });
        eoiSegment.Should().NotBeNull();
    }

    [Fact]
    public void JpegFormat_MergedTables_DecodesEveryDqtAndDhtTable()
    {
        var data = JpegTestDataGenerator.CreateJpegWithMergedTables();
        var format = new YamlFormatLoader().Load(JpegFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var segments = decoded.Children[1].Should().BeOfType<DecodedArray>().Subject.Elements.Cast<DecodedStruct>().ToList();
        DecodedArray Tables(int markerType) =>
            (DecodedArray)Child(Child(Child(segments.First(s => (s.Children[1] as DecodedInteger)?.Value == markerType), "body"), "body"), "tables");

        var dqt = Tables(0xDB);
        dqt.Elements.Should().HaveCount(2);
        var qt0 = (DecodedStruct)dqt.Elements[0];
        ((DecodedVirtual)Child(qt0, "precision")).Value.Should().Be(0L);
        ((DecodedBytes)Child(qt0, "table_data")).RawBytes.Length.Should().Be(64);
        var qt1 = (DecodedStruct)dqt.Elements[1];
        ((DecodedVirtual)Child(qt1, "precision")).Value.Should().Be(1L);
        ((DecodedVirtual)Child(qt1, "table_id")).Value.Should().Be(1L);
        ((DecodedBytes)Child(qt1, "table_data")).RawBytes.Length.Should().Be(128);

        var dht = Tables(0xC4);
        dht.Elements.Should().HaveCount(2);
        var dc = (DecodedStruct)dht.Elements[0];
        ((DecodedVirtual)Child(dc, "table_class")).Value.Should().Be(0L);
        ((DecodedVirtual)Child(dc, "total_codes")).Value.Should().Be(2L);
        ((DecodedBytes)Child(dc, "values")).RawBytes.ToArray().Should().Equal(0x00, 0x01);
        var ac = (DecodedStruct)dht.Elements[1];
        ((DecodedVirtual)Child(ac, "table_class")).Value.Should().Be(1L);
        ((DecodedVirtual)Child(ac, "table_id")).Value.Should().Be(1L);
        ((DecodedVirtual)Child(ac, "total_codes")).Value.Should().Be(3L);
        ((DecodedBytes)Child(ac, "values")).RawBytes.ToArray().Should().Equal(0x01, 0x00, 0xF0);

        // DHT の後ろの EOI までずれずに読めている
        ((DecodedInteger)segments[^1].Children[1]).EnumLabel.Should().Be("EOI");
    }

    private static DecodedNode Child(DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);
}
