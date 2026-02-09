using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class BinaryDecoderTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void Decode_SimpleUInt32()
    {
        var format = CreateFormat("main", new FieldDefinition
        {
            Name = "magic",
            Type = FieldType.UInt32,
        });
        var data = new byte[] { 0x00, 0x00, 0x00, 0x2A }; // 42

        var result = _decoder.Decode(data, format);

        result.Children.Should().HaveCount(1);
        var field = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        field.Name.Should().Be("magic");
        field.Value.Should().Be(42);
    }

    [Fact]
    public void Decode_BytesWithExpected_Valid()
    {
        var format = CreateFormat("main", new FieldDefinition
        {
            Name = "sig",
            Type = FieldType.Bytes,
            Size = 4,
            Expected = [0x89, 0x50, 0x4E, 0x47],
        });
        var data = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        var result = _decoder.Decode(data, format);

        var field = result.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        field.ValidationPassed.Should().BeTrue();
    }

    [Fact]
    public void Decode_BytesWithExpected_Invalid()
    {
        var format = CreateFormat("main", new FieldDefinition
        {
            Name = "sig",
            Type = FieldType.Bytes,
            Size = 4,
            Expected = [0x89, 0x50, 0x4E, 0x47],
        });
        var data = new byte[] { 0x00, 0x00, 0x00, 0x00 };

        var result = _decoder.Decode(data, format);

        var field = result.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        field.ValidationPassed.Should().BeFalse();
    }

    [Fact]
    public void Decode_DynamicSizeBytes()
    {
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "length",
                Type = FieldType.UInt8,
            },
            new FieldDefinition
            {
                Name = "data",
                Type = FieldType.Bytes,
                SizeExpression = ExpressionParser.Parse("{length}"),
            });
        var data = new byte[] { 0x03, 0xAA, 0xBB, 0xCC };

        var result = _decoder.Decode(data, format);

        var bytesField = result.Children[1].Should().BeOfType<DecodedBytes>().Subject;
        bytesField.RawBytes.ToArray().Should().BeEquivalentTo(new byte[] { 0xAA, 0xBB, 0xCC });
    }

    [Fact]
    public void Decode_AsciiField()
    {
        var format = CreateFormat("main", new FieldDefinition
        {
            Name = "type",
            Type = FieldType.Ascii,
            Size = 4,
        });
        var data = "IHDR"u8.ToArray();

        var result = _decoder.Decode(data, format);

        var field = result.Children[0].Should().BeOfType<DecodedString>().Subject;
        field.Value.Should().Be("IHDR");
        field.Encoding.Should().Be("ascii");
    }

    [Fact]
    public void Decode_WithEnum()
    {
        var enums = new Dictionary<string, EnumDefinition>
        {
            ["color"] = new()
            {
                Name = "color",
                Entries = [new EnumEntry(2, "truecolor", "RGB color")]
            },
        };
        var format = new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = enums,
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["main"] = new()
                {
                    Name = "main",
                    Fields = [new FieldDefinition
                    {
                        Name = "color_type",
                        Type = FieldType.UInt8,
                        EnumRef = "color",
                    }],
                },
            },
            RootStruct = "main",
        };
        var data = new byte[] { 0x02 };

        var result = _decoder.Decode(data, format);

        var field = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        field.Value.Should().Be(2);
        field.EnumLabel.Should().Be("truecolor");
        field.EnumDescription.Should().Be("RGB color");
    }

    [Fact]
    public void Decode_RepeatUntilEof()
    {
        var format = CreateFormat("main", new FieldDefinition
        {
            Name = "items",
            Type = FieldType.UInt8,
            Repeat = new RepeatMode.UntilEof(),
        });
        var data = new byte[] { 0x01, 0x02, 0x03 };

        var result = _decoder.Decode(data, format);

        var array = result.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);
    }

    [Fact]
    public void Decode_RepeatCount()
    {
        var format = CreateFormat("main",
            new FieldDefinition { Name = "count", Type = FieldType.UInt8 },
            new FieldDefinition
            {
                Name = "items",
                Type = FieldType.UInt8,
                Repeat = new RepeatMode.Count(ExpressionParser.Parse("{count}")),
            });
        var data = new byte[] { 0x02, 0xAA, 0xBB };

        var result = _decoder.Decode(data, format);

        var array = result.Children[1].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);
    }

    [Fact]
    public void Decode_Switch()
    {
        var format = new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["main"] = new()
                {
                    Name = "main",
                    Fields =
                    [
                        new FieldDefinition { Name = "type", Type = FieldType.UInt8 },
                        new FieldDefinition
                        {
                            Name = "body",
                            Type = FieldType.Switch,
                            Size = 4,
                            SwitchOn = ExpressionParser.Parse("{type}"),
                            SwitchCases =
                            [
                                new SwitchCase(ExpressionParser.Parse("{1}"), "type_a"),
                                new SwitchCase(ExpressionParser.Parse("{2}"), "type_b"),
                            ],
                            SwitchDefault = "raw",
                        },
                    ],
                },
                ["type_a"] = new()
                {
                    Name = "type_a",
                    Fields = [new FieldDefinition { Name = "value", Type = FieldType.UInt32 }],
                },
                ["type_b"] = new()
                {
                    Name = "type_b",
                    Fields = [new FieldDefinition { Name = "value", Type = FieldType.UInt16 }],
                },
                ["raw"] = new()
                {
                    Name = "raw",
                    Fields = [new FieldDefinition { Name = "data", Type = FieldType.Bytes, SizeRemaining = true }],
                },
            },
            RootStruct = "main",
        };

        // type=1 → type_a, body is 4 bytes containing uint32 = 0x12345678
        var data = new byte[] { 0x01, 0x12, 0x34, 0x56, 0x78 };

        var result = _decoder.Decode(data, format);

        var body = result.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        body.StructType.Should().Be("type_a");
        var value = body.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        value.Value.Should().Be(0x12345678);
    }

    [Fact]
    public void Decode_NestedStruct()
    {
        var format = new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["main"] = new()
                {
                    Name = "main",
                    Fields = [new FieldDefinition
                    {
                        Name = "header",
                        Type = FieldType.Struct,
                        StructRef = "header",
                    }],
                },
                ["header"] = new()
                {
                    Name = "header",
                    Fields =
                    [
                        new FieldDefinition { Name = "version", Type = FieldType.UInt8 },
                        new FieldDefinition { Name = "flags", Type = FieldType.UInt8 },
                    ],
                },
            },
            RootStruct = "main",
        };
        var data = new byte[] { 0x01, 0x02 };

        var result = _decoder.Decode(data, format);

        var header = result.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        header.StructType.Should().Be("header");
        header.Children.Should().HaveCount(2);
    }

    [Fact]
    public void Decode_SizeRemaining()
    {
        var format = CreateFormat("main",
            new FieldDefinition { Name = "tag", Type = FieldType.UInt8 },
            new FieldDefinition { Name = "rest", Type = FieldType.Bytes, SizeRemaining = true });
        var data = new byte[] { 0xFF, 0x01, 0x02, 0x03, 0x04 };

        var result = _decoder.Decode(data, format);

        var rest = result.Children[1].Should().BeOfType<DecodedBytes>().Subject;
        rest.RawBytes.Length.Should().Be(4);
    }

    [Fact]
    public void Decode_SizeRemainingExpression_DecodesCorrectly()
    {
        // 1バイトタグ + 残り(7バイト) のうち size: "{remaining - 2}" → 5バイト + 末尾2バイト
        var format = CreateFormat("main",
            new FieldDefinition { Name = "tag", Type = FieldType.UInt8 },
            new FieldDefinition
            {
                Name = "body",
                Type = FieldType.Bytes,
                SizeExpression = ExpressionParser.Parse("{remaining - 2}"),
            },
            new FieldDefinition { Name = "footer", Type = FieldType.UInt16 });
        var data = new byte[] { 0xFF, 0x01, 0x02, 0x03, 0x04, 0x05, 0x00, 0x06 };

        var result = _decoder.Decode(data, format);

        result.Children.Should().HaveCount(3);
        var body = result.Children[1].Should().BeOfType<DecodedBytes>().Subject;
        body.RawBytes.Length.Should().Be(5);
        var footer = result.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        footer.Name.Should().Be("footer");
    }

    private static FormatDefinition CreateFormat(string rootName, params FieldDefinition[] fields)
    {
        return new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                [rootName] = new()
                {
                    Name = rootName,
                    Fields = fields.ToList(),
                },
            },
            RootStruct = rootName,
        };
    }
}
