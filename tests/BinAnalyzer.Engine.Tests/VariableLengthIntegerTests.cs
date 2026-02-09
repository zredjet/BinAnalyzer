using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class VariableLengthIntegerTests
{
    private readonly BinaryDecoder _decoder = new();

    // --- ULEB128 ---

    [Fact]
    public void ULeb128_SingleByte_Zero()
    {
        var format = CreateFormat(new FieldDefinition { Name = "v", Type = FieldType.ULeb128 });
        var result = _decoder.Decode(new byte[] { 0x00 }, format);
        var field = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        field.Value.Should().Be(0);
        field.Size.Should().Be(1);
    }

    [Fact]
    public void ULeb128_SingleByte_127()
    {
        var format = CreateFormat(new FieldDefinition { Name = "v", Type = FieldType.ULeb128 });
        var result = _decoder.Decode(new byte[] { 0x7F }, format);
        var field = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        field.Value.Should().Be(127);
        field.Size.Should().Be(1);
    }

    [Fact]
    public void ULeb128_TwoBytes_128()
    {
        var format = CreateFormat(new FieldDefinition { Name = "v", Type = FieldType.ULeb128 });
        var result = _decoder.Decode(new byte[] { 0x80, 0x01 }, format);
        var field = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        field.Value.Should().Be(128);
        field.Size.Should().Be(2);
    }

    [Fact]
    public void ULeb128_MultiBytes_624485()
    {
        var format = CreateFormat(new FieldDefinition { Name = "v", Type = FieldType.ULeb128 });
        var result = _decoder.Decode(new byte[] { 0xE5, 0x8E, 0x26 }, format);
        var field = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        field.Value.Should().Be(624485);
        field.Size.Should().Be(3);
    }

    [Fact]
    public void ULeb128_ExceedsMaxBytes_ThrowsException()
    {
        var format = CreateFormat(new FieldDefinition { Name = "v", Type = FieldType.ULeb128 });
        // 11 bytes all with MSB=1 (except we need 11 continuation bytes)
        var data = new byte[11];
        for (int i = 0; i < 11; i++)
            data[i] = 0x80;

        var act = () => _decoder.Decode(data, format);
        act.Should().Throw<Exception>().WithMessage("*ULEB128*exceeds*");
    }

    // --- SLEB128 ---

    [Fact]
    public void SLeb128_PositiveValue_2()
    {
        var format = CreateFormat(new FieldDefinition { Name = "v", Type = FieldType.SLeb128 });
        var result = _decoder.Decode(new byte[] { 0x02 }, format);
        var field = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        field.Value.Should().Be(2);
        field.Size.Should().Be(1);
    }

    [Fact]
    public void SLeb128_NegativeValue_Minus1()
    {
        var format = CreateFormat(new FieldDefinition { Name = "v", Type = FieldType.SLeb128 });
        var result = _decoder.Decode(new byte[] { 0x7F }, format);
        var field = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        field.Value.Should().Be(-1);
        field.Size.Should().Be(1);
    }

    [Fact]
    public void SLeb128_NegativeValue_Minus127()
    {
        var format = CreateFormat(new FieldDefinition { Name = "v", Type = FieldType.SLeb128 });
        var result = _decoder.Decode(new byte[] { 0x81, 0x7F }, format);
        var field = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        field.Value.Should().Be(-127);
        field.Size.Should().Be(2);
    }

    [Fact]
    public void SLeb128_ExceedsMaxBytes_ThrowsException()
    {
        var format = CreateFormat(new FieldDefinition { Name = "v", Type = FieldType.SLeb128 });
        var data = new byte[11];
        for (int i = 0; i < 11; i++)
            data[i] = 0x80;

        var act = () => _decoder.Decode(data, format);
        act.Should().Throw<Exception>().WithMessage("*SLEB128*exceeds*");
    }

    // --- VLQ ---

    [Fact]
    public void Vlq_SingleByte_Zero()
    {
        var format = CreateFormat(new FieldDefinition { Name = "v", Type = FieldType.Vlq });
        var result = _decoder.Decode(new byte[] { 0x00 }, format);
        var field = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        field.Value.Should().Be(0);
        field.Size.Should().Be(1);
    }

    [Fact]
    public void Vlq_SingleByte_127()
    {
        var format = CreateFormat(new FieldDefinition { Name = "v", Type = FieldType.Vlq });
        var result = _decoder.Decode(new byte[] { 0x7F }, format);
        var field = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        field.Value.Should().Be(127);
        field.Size.Should().Be(1);
    }

    [Fact]
    public void Vlq_TwoBytes_128()
    {
        var format = CreateFormat(new FieldDefinition { Name = "v", Type = FieldType.Vlq });
        var result = _decoder.Decode(new byte[] { 0x81, 0x00 }, format);
        var field = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        field.Value.Should().Be(128);
        field.Size.Should().Be(2);
    }

    [Fact]
    public void Vlq_MultiBytes_480()
    {
        var format = CreateFormat(new FieldDefinition { Name = "v", Type = FieldType.Vlq });
        var result = _decoder.Decode(new byte[] { 0x83, 0x60 }, format);
        var field = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        field.Value.Should().Be(480);
        field.Size.Should().Be(2);
    }

    [Fact]
    public void Vlq_ExceedsMaxBytes_ThrowsException()
    {
        var format = CreateFormat(new FieldDefinition { Name = "v", Type = FieldType.Vlq });
        var data = new byte[11];
        for (int i = 0; i < 11; i++)
            data[i] = 0x80;

        var act = () => _decoder.Decode(data, format);
        act.Should().Throw<Exception>().WithMessage("*VLQ*exceeds*");
    }

    // --- Enum reference ---

    [Fact]
    public void ULeb128_EnumRef_ResolvesLabel()
    {
        var format = new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>
            {
                ["my_enum"] = new()
                {
                    Name = "my_enum",
                    Entries = [new EnumEntry(1, "one"), new EnumEntry(128, "big")],
                },
            },
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["main"] = new()
                {
                    Name = "main",
                    Fields = [new FieldDefinition { Name = "v", Type = FieldType.ULeb128, EnumRef = "my_enum" }],
                },
            },
            RootStruct = "main",
        };

        // 128 in ULEB128 = 0x80, 0x01
        var result = _decoder.Decode(new byte[] { 0x80, 0x01 }, format);
        var field = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        field.Value.Should().Be(128);
        field.EnumLabel.Should().Be("big");
    }

    // --- Variable reference in expressions ---

    [Fact]
    public void ULeb128_VariableBinding_UsableInExpression()
    {
        var format = CreateFormat(
            new FieldDefinition { Name = "length", Type = FieldType.ULeb128 },
            new FieldDefinition
            {
                Name = "data",
                Type = FieldType.Bytes,
                SizeExpression = ExpressionParser.Parse("{length}"),
            });

        // length = 3 (ULEB128: 0x03), then 3 bytes of data
        var result = _decoder.Decode(new byte[] { 0x03, 0xAA, 0xBB, 0xCC }, format);
        var length = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        length.Value.Should().Be(3);
        var data = result.Children[1].Should().BeOfType<DecodedBytes>().Subject;
        data.Size.Should().Be(3);
    }

    [Fact]
    public void ULeb128_FollowedByMoreFields_PositionCorrect()
    {
        var format = CreateFormat(
            new FieldDefinition { Name = "v", Type = FieldType.ULeb128 },
            new FieldDefinition { Name = "after", Type = FieldType.UInt8 });

        // v = 128 (0x80, 0x01), after = 0x42
        var result = _decoder.Decode(new byte[] { 0x80, 0x01, 0x42 }, format);
        var v = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        v.Value.Should().Be(128);
        v.Size.Should().Be(2);
        var after = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        after.Value.Should().Be(0x42);
    }

    private static FormatDefinition CreateFormat(params FieldDefinition[] fields)
    {
        return new FormatDefinition
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
                    Fields = fields.ToList(),
                },
            },
            RootStruct = "main",
        };
    }
}
