using System.Text;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class NullTerminatedStringTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void Decode_AsciiZ_ReadsUntilNull()
    {
        var format = CreateFormat("main", new FieldDefinition
        {
            Name = "label",
            Type = FieldType.AsciiZ,
        });
        var data = Encoding.ASCII.GetBytes("Hello\0extra");

        var result = _decoder.Decode(data, format);

        var field = result.Children[0].Should().BeOfType<DecodedString>().Subject;
        field.Value.Should().Be("Hello");
        field.Name.Should().Be("label");
        field.Size.Should().Be(6); // 5 chars + NUL
    }

    [Fact]
    public void Decode_AsciiZ_EmptyString()
    {
        var format = CreateFormat("main", new FieldDefinition
        {
            Name = "label",
            Type = FieldType.AsciiZ,
        });
        var data = new byte[] { 0x00, 0x01, 0x02 };

        var result = _decoder.Decode(data, format);

        var field = result.Children[0].Should().BeOfType<DecodedString>().Subject;
        field.Value.Should().Be("");
        field.Size.Should().Be(1); // just NUL
    }

    [Fact]
    public void Decode_AsciiZ_FollowedByMoreFields()
    {
        var format = CreateFormat("main",
            new FieldDefinition { Name = "label", Type = FieldType.AsciiZ },
            new FieldDefinition { Name = "value", Type = FieldType.UInt8 });
        var data = new byte[] { (byte)'A', (byte)'B', 0x00, 0x42 };

        var result = _decoder.Decode(data, format);

        var str = result.Children[0].Should().BeOfType<DecodedString>().Subject;
        str.Value.Should().Be("AB");
        str.Size.Should().Be(3);

        var value = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        value.Value.Should().Be(0x42);
    }

    [Fact]
    public void Decode_Utf8Z_ReadsUntilNull()
    {
        var format = CreateFormat("main", new FieldDefinition
        {
            Name = "label",
            Type = FieldType.Utf8Z,
        });
        // "日本" in UTF-8 is 6 bytes, followed by NUL
        var text = Encoding.UTF8.GetBytes("日本");
        var data = new byte[text.Length + 1];
        text.CopyTo(data, 0);
        data[text.Length] = 0x00;

        var result = _decoder.Decode(data, format);

        var field = result.Children[0].Should().BeOfType<DecodedString>().Subject;
        field.Value.Should().Be("日本");
        field.Size.Should().Be(7); // 6 bytes + NUL
    }

    [Fact]
    public void Decode_AsciiZ_NoNullInScope_ReadsToEnd()
    {
        var format = CreateFormat("main", new FieldDefinition
        {
            Name = "label",
            Type = FieldType.AsciiZ,
        });
        // No null terminator — reads to end of data
        var data = Encoding.ASCII.GetBytes("NoNull");

        var result = _decoder.Decode(data, format);

        var field = result.Children[0].Should().BeOfType<DecodedString>().Subject;
        field.Value.Should().Be("NoNull");
        field.Size.Should().Be(6);
    }

    [Fact]
    public void Decode_AsciiZ_VariableBinding()
    {
        // The value should be bound to scope as a string variable
        var format = CreateFormat("main",
            new FieldDefinition { Name = "name", Type = FieldType.AsciiZ },
            new FieldDefinition
            {
                Name = "check",
                Type = FieldType.UInt8,
                Condition = Core.Expressions.ExpressionParser.Parse("{name == 'OK'}"),
            });
        var data = new byte[] { (byte)'O', (byte)'K', 0x00, 0xFF };

        var result = _decoder.Decode(data, format);

        // "name" value is "OK" which matches condition, so check field should be present
        result.Children.Should().HaveCount(2);
        var check = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        check.Value.Should().Be(0xFF);
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
