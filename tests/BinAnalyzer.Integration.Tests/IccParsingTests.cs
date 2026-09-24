using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class IccParsingTests
{
    private static readonly string IccFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "icc.bdef.yaml");

    [Fact]
    public void IccFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(IccFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void IccFormat_DecodesMinimalIcc()
    {
        var data = IccTestDataGenerator.CreateMinimalIcc();
        var format = new YamlFormatLoader().Load(IccFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("ICC");
        decoded.Children.Should().HaveCount(2);
        decoded.Children[0].Name.Should().Be("header");
        decoded.Children[1].Name.Should().Be("tag_table");
    }

    [Fact]
    public void IccFormat_Header_DecodesCorrectly()
    {
        var data = IccTestDataGenerator.CreateMinimalIcc();
        var format = new YamlFormatLoader().Load(IccFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var header = decoded.Children[0].Should().BeOfType<DecodedStruct>().Subject;

        var profileSize = header.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        profileSize.Name.Should().Be("profile_size");
        profileSize.Value.Should().Be(132);

        var deviceClass = header.Children[3].Should().BeOfType<DecodedInteger>().Subject;
        deviceClass.Name.Should().Be("device_class");
        deviceClass.EnumLabel.Should().Be("Monitor");

        var colorSpace = header.Children[4].Should().BeOfType<DecodedInteger>().Subject;
        colorSpace.Name.Should().Be("color_space");
        colorSpace.EnumLabel.Should().Be("RGB");

        var signature = header.Children[7].Should().BeOfType<DecodedString>().Subject;
        signature.Name.Should().Be("signature");
        signature.Value.Should().Be("acsp");

        var renderingIntent = header.Children[13].Should().BeOfType<DecodedInteger>().Subject;
        renderingIntent.Name.Should().Be("rendering_intent");
        renderingIntent.EnumLabel.Should().Be("Perceptual");
    }

    [Fact]
    public void IccFormat_DescTag_DecodesCorrectly()
    {
        var data = IccTestDataGenerator.CreateIccWithTags();
        var format = new YamlFormatLoader().Load(IccFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var tagTable = decoded.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var tags = tagTable.Children[1].Should().BeOfType<DecodedArray>().Subject;
        tags.Elements.Should().HaveCount(2);

        // tags[0] = desc tag_entry
        var descEntry = tags.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var sig = descEntry.Children[0].Should().BeOfType<DecodedString>().Subject;
        sig.Value.Should().Be("desc");

        // data → 型シグネチャ（tag_type）で振り分け → desc_tag_data（REQ-188）
        ((DecodedString)descEntry.Children.Single(c => c.Name == "tag_type")).Value.Should().Be("desc");
        var descData = descEntry.Children.Single(c => c.Name == "data").Should().BeOfType<DecodedStruct>().Subject;
        var typeSig = descData.Children[0].Should().BeOfType<DecodedString>().Subject;
        typeSig.Value.Should().Be("desc");

        var asciiLength = descData.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        asciiLength.Value.Should().Be(12);

        var asciiDesc = descData.Children[3].Should().BeOfType<DecodedString>().Subject;
        asciiDesc.Value.Should().Be("Test Profile");
    }

    [Fact]
    public void IccFormat_XyzTag_DecodesCorrectly()
    {
        var data = IccTestDataGenerator.CreateIccWithTags();
        var format = new YamlFormatLoader().Load(IccFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var tagTable = decoded.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var tags = tagTable.Children[1].Should().BeOfType<DecodedArray>().Subject;

        // tags[1] = XYZ  tag_entry
        var xyzEntry = tags.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        var sig = xyzEntry.Children[0].Should().BeOfType<DecodedString>().Subject;
        sig.Value.Should().Be("XYZ ");

        // data → 型シグネチャで振り分け → xyz_tag_data。XYZType は XYZ 値の配列（REQ-188）
        var xyzData = xyzEntry.Children.Single(c => c.Name == "data").Should().BeOfType<DecodedStruct>().Subject;
        var typeSig = xyzData.Children[0].Should().BeOfType<DecodedString>().Subject;
        typeSig.Value.Should().Be("XYZ ");

        var values = xyzData.Children.Single(c => c.Name == "values").Should().BeOfType<DecodedArray>().Subject;
        values.Elements.Should().HaveCount(1);
        var xyz = (DecodedStruct)values.Elements[0];
        var x = xyz.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        x.Name.Should().Be("x");
        x.Value.Should().Be(0x0000F6D6);

        var y = xyz.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        y.Name.Should().Be("y");
        y.Value.Should().Be(0x00010000);
    }

    [Fact]
    public void IccFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = IccTestDataGenerator.CreateMinimalIcc();
        var format = new YamlFormatLoader().Load(IccFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("ICC");
        output.Should().Contain("header");
        output.Should().Contain("Monitor");
        output.Should().Contain("RGB");
        output.Should().Contain("Perceptual");
        output.Should().Contain("tag_table");
    }

    [Fact]
    public void IccV4Profile_TagsAreDecodedByTypeSignature()
    {
        // REQ-188: タグの名前ではなくデータ先頭の型シグネチャで振り分ける（v4 の mluc、para、sf32、chrm）
        var data = IccTestDataGenerator.CreateV4ProfileWithTypedTags();
        var decoded = new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(IccFormatPath));

        var tags = ((DecodedArray)((DecodedStruct)decoded.Children[1]).Children[1]).Elements.Cast<DecodedStruct>().ToList();
        DecodedStruct DataOf(string signature) =>
            (DecodedStruct)tags.Single(t => ((DecodedString)t.Children[0]).Value == signature).Children.Single(c => c.Name == "data");

        var desc = DataOf("desc");
        desc.StructType.Should().Be("mluc_tag_data", "v4 の 'desc' タグは mluc 型");
        var record = (DecodedStruct)((DecodedArray)desc.Children.Single(c => c.Name == "records")).Elements[0];
        ((DecodedString)record.Children.Single(c => c.Name == "language")).Value.Should().Be("en");
        ((DecodedString)record.Children.Single(c => c.Name == "text")).Value.Should().Be("Test v4");

        var trc = DataOf("rTRC");
        trc.StructType.Should().Be("para_tag_data");
        ((DecodedArray)trc.Children.Single(c => c.Name == "parameters")).Elements.Should().HaveCount(1);
        DataOf("chad").StructType.Should().Be("sf32_tag_data");
        ((DecodedArray)DataOf("chad").Children.Single(c => c.Name == "values")).Elements.Should().HaveCount(9);
    }
}
