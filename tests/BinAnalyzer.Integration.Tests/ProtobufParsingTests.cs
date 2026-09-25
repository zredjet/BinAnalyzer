using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class ProtobufParsingTests
{
    private static readonly string ProtobufFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "protobuf.bdef.yaml");

    [Fact]
    public void ProtobufFormat_LoadsWithoutErrors()
    {
        var result = FormatValidator.Validate(new YamlFormatLoader().Load(ProtobufFormatPath));
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ProtobufFormat_MinimalMessage_DecodesFields()
    {
        var fields = Fields(ProtobufTestDataGenerator.CreateMinimalProtobuf());

        fields.Select(f => f.Child("field_number").Int()).Should().Equal(1, 2, 3);
        fields[0].Child("varint").Int().Should().Be(150);
        fields[1].Child("text").Str().Should().Be("Hello");
        fields[2].Child("fixed32_value").Int().Should().Be(42);
    }

    [Fact]
    public void ProtobufFormat_Varints_ShowTwosComplementAndZigZag()
    {
        var fields = Fields(ProtobufTestDataGenerator.CreateProtobufWithAllWireTypes());

        fields[0].Child("varint").Int().Should().Be(-5);                  // int32 の -5 は 10 バイトの varint
        fields[0].Size.Should().Be(11);
        fields[1].Child("zigzag_value").Int().Should().Be(-3);            // sint32
        fields[^1].Child("field_number").Int().Should().Be(300);
        fields[^1].Child("varint").Int().Should().Be(1L << 40);
    }

    [Fact]
    public void ProtobufFormat_FixedValues_ShowIntegerAndFloat()
    {
        var fields = Fields(ProtobufTestDataGenerator.CreateProtobufWithAllWireTypes());

        var d = fields.Single(f => f.Child("field_number").Int() == 6);
        ((DecodedFloat)d.Child("double_value")).Value.Should().Be(3.5);
        var f7 = fields.Single(f => f.Child("field_number").Int() == 7);
        ((DecodedFloat)f7.Child("float_value")).Value.Should().Be(-1.25);
        fields.Single(f => f.Child("field_number").Int() == 8).Child("fixed32_value").Int().Should().Be(0xDEADBEEF);
    }

    [Fact]
    public void ProtobufFormat_LengthDelimited_ShowsTextAndBytes()
    {
        var fields = Fields(ProtobufTestDataGenerator.CreateProtobufWithAllWireTypes());

        var text = fields.Single(f => f.Child("field_number").Int() == 3);
        text.Child("wire_type").Label().Should().Be("LEN");
        text.Child("text").Str().Should().Be("こんにちは");
        text.Child("delimited_data").Size.Should().Be(15);
        var packed = fields.Single(f => f.Child("field_number").Int() == 12);
        ((DecodedBytes)packed.Child("delimited_data")).RawBytes.ToArray().Should().Equal(0x01, 0x96, 0x01, 0x03);
    }

    [Fact]
    public void ProtobufFormat_Group_DecodesNestedFieldsUntilEndGroup()
    {
        var fields = Fields(ProtobufTestDataGenerator.CreateProtobufWithAllWireTypes());

        var group = fields.Single(f => f.Child("field_number").Int() == 10);
        group.Child("wire_type").Label().Should().Be("SGROUP");
        var inner = group.Child("group").Child("fields").Elements();
        inner.Should().HaveCount(2);
        inner[0].Child("field_number").Int().Should().Be(11);
        inner[0].Child("text").Str().Should().Be("g");
        inner[1].Child("wire_type").Label().Should().Be("EGROUP");
        // グループの後ろも続けて読む
        fields.Select(f => f.Child("field_number").Int()).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 300);
    }

    private static IReadOnlyList<DecodedNode> Fields(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(ProtobufFormatPath)).Child("fields").Elements();
}
