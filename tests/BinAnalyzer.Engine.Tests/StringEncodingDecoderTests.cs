using System.Text;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class StringEncodingDecoderTests
{
    private static FormatDefinition CreateStringFormat(FieldType type, int size)
    {
        return new FormatDefinition
        {
            Name = "test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["root"] = new()
                {
                    Name = "root",
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "text",
                            Type = type,
                            Size = size,
                        },
                    ],
                },
            },
            RootStruct = "root",
        };
    }

    [Fact]
    public void Decode_Utf16Le_DecodesCorrectly()
    {
        var text = "Hello";
        var bytes = Encoding.Unicode.GetBytes(text);
        var format = CreateStringFormat(FieldType.Utf16Le, bytes.Length);

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(bytes, format);

        var str = result.Children[0].Should().BeOfType<DecodedString>().Subject;
        str.Value.Should().Be("Hello");
        str.Encoding.Should().Be("utf16le");
    }

    [Fact]
    public void Decode_Utf16Be_DecodesCorrectly()
    {
        var text = "Hello";
        var bytes = Encoding.BigEndianUnicode.GetBytes(text);
        var format = CreateStringFormat(FieldType.Utf16Be, bytes.Length);

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(bytes, format);

        var str = result.Children[0].Should().BeOfType<DecodedString>().Subject;
        str.Value.Should().Be("Hello");
        str.Encoding.Should().Be("utf16be");
    }

    [Fact]
    public void Decode_Latin1_DecodesCorrectly()
    {
        // Latin-1: 0xE9 = 'é'
        var bytes = new byte[] { 0x63, 0x61, 0x66, 0xE9 }; // "café"
        var format = CreateStringFormat(FieldType.Latin1, bytes.Length);

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(bytes, format);

        var str = result.Children[0].Should().BeOfType<DecodedString>().Subject;
        str.Value.Should().Be("caf\u00e9");
        str.Encoding.Should().Be("latin1");
    }

    [Fact]
    public void Decode_ShiftJis_DecodesCorrectly()
    {
        // Shift_JIS: "テスト" = 0x83 0x65 0x83 0x58 0x83 0x67
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var sjis = Encoding.GetEncoding("shift_jis");
        var bytes = sjis.GetBytes("テスト");
        var format = CreateStringFormat(FieldType.ShiftJis, bytes.Length);

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(bytes, format);

        var str = result.Children[0].Should().BeOfType<DecodedString>().Subject;
        str.Value.Should().Be("テスト");
        str.Encoding.Should().Be("sjis");
    }

    [Fact]
    public void Decode_Utf16Le_Japanese_DecodesCorrectly()
    {
        var text = "日本語";
        var bytes = Encoding.Unicode.GetBytes(text);
        var format = CreateStringFormat(FieldType.Utf16Le, bytes.Length);

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(bytes, format);

        var str = result.Children[0].Should().BeOfType<DecodedString>().Subject;
        str.Value.Should().Be("日本語");
    }

    [Fact]
    public void Decode_StringField_SetsVariable()
    {
        // 文字列フィールドの後に別フィールドがあり、変数バインディングが機能することを確認
        var format = new FormatDefinition
        {
            Name = "test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["root"] = new()
                {
                    Name = "root",
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "label",
                            Type = FieldType.Latin1,
                            Size = 4,
                        },
                        new FieldDefinition
                        {
                            Name = "value",
                            Type = FieldType.UInt8,
                        },
                    ],
                },
            },
            RootStruct = "root",
        };

        var bytes = new byte[] { 0x41, 0x42, 0x43, 0x44, 0x0A };
        var decoder = new BinaryDecoder();
        var result = decoder.Decode(bytes, format);

        result.Children.Should().HaveCount(2);
        var str = result.Children[0].Should().BeOfType<DecodedString>().Subject;
        str.Value.Should().Be("ABCD");
        var intNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        intNode.Value.Should().Be(10);
    }
}
