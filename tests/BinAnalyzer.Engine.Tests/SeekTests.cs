using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class SeekTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void Seek_JumpsToAbsoluteOffset()
    {
        // data: [0x00, 0x00, 0x00, 0x00, 0xAB]
        // seek to offset 4, read uint8 => 0xAB
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "value",
                Type = FieldType.UInt8,
                SeekExpression = ExpressionParser.Parse("{4}"),
            });

        var data = new byte[] { 0x00, 0x00, 0x00, 0x00, 0xAB };
        var result = _decoder.Decode(data, format);

        var node = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        node.Value.Should().Be(0xAB);
        node.Offset.Should().Be(4);
    }

    [Fact]
    public void Seek_WithFieldReference()
    {
        // data: [offset=0x04, padding, padding, padding, 0xCD]
        // first field: uint8 at offset 0 => 4
        // second field: seek to {offset}, read uint8 => 0xCD
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "offset",
                Type = FieldType.UInt8,
            },
            new FieldDefinition
            {
                Name = "value",
                Type = FieldType.UInt8,
                SeekExpression = ExpressionParser.Parse("{offset}"),
            });

        var data = new byte[] { 0x04, 0x00, 0x00, 0x00, 0xCD };
        var result = _decoder.Decode(data, format);

        var node = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        node.Value.Should().Be(0xCD);
    }

    [Fact]
    public void Seek_WithArithmeticExpression()
    {
        // data: [base=0x02, padding, padding, 0xEF, padding]
        // seek to {base + 1} = 3, read uint8 => 0xEF
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "base_offset",
                Type = FieldType.UInt8,
            },
            new FieldDefinition
            {
                Name = "value",
                Type = FieldType.UInt8,
                SeekExpression = ExpressionParser.Parse("{base_offset + 1}"),
            });

        var data = new byte[] { 0x02, 0x00, 0x00, 0xEF, 0x00 };
        var result = _decoder.Decode(data, format);

        var node = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        node.Value.Should().Be(0xEF);
    }

    [Fact]
    public void SeekRestore_ReturnsToPreviousPosition()
    {
        // data: [0x04, 0xAA, 0xBB, 0xCC, 0xDD]
        // field1: uint8 at 0 => 4 (offset value)
        // field2: seek to 4, read uint8 => 0xDD, restore to position 1
        // field3: uint8 at 1 => 0xAA
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "ptr",
                Type = FieldType.UInt8,
            },
            new FieldDefinition
            {
                Name = "remote_value",
                Type = FieldType.UInt8,
                SeekExpression = ExpressionParser.Parse("{ptr}"),
                SeekRestore = true,
            },
            new FieldDefinition
            {
                Name = "next",
                Type = FieldType.UInt8,
            });

        var data = new byte[] { 0x04, 0xAA, 0xBB, 0xCC, 0xDD };
        var result = _decoder.Decode(data, format);

        result.Children.Should().HaveCount(3);

        var remoteNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        remoteNode.Value.Should().Be(0xDD);

        // After restore, next field should be at position 1 (right after ptr)
        var nextNode = result.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        nextNode.Offset.Should().Be(1);
        nextNode.Value.Should().Be(0xAA);
    }

    [Fact]
    public void SeekRestore_False_ContinuesFromNewPosition()
    {
        // data: [0x03, 0xAA, 0xBB, 0xCC, 0xDD]
        // field1: uint8 at 0 => 3
        // field2: seek to 3, read uint8 => 0xCC (no restore)
        // field3: uint8 at 4 => 0xDD (continues from new position)
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "ptr",
                Type = FieldType.UInt8,
            },
            new FieldDefinition
            {
                Name = "remote_value",
                Type = FieldType.UInt8,
                SeekExpression = ExpressionParser.Parse("{ptr}"),
                SeekRestore = false,
            },
            new FieldDefinition
            {
                Name = "next",
                Type = FieldType.UInt8,
            });

        var data = new byte[] { 0x03, 0xAA, 0xBB, 0xCC, 0xDD };
        var result = _decoder.Decode(data, format);

        result.Children.Should().HaveCount(3);

        var remoteNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        remoteNode.Value.Should().Be(0xCC);

        var nextNode = result.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        nextNode.Offset.Should().Be(4);
        nextNode.Value.Should().Be(0xDD);
    }

    [Fact]
    public void Seek_NegativeOffset_ThrowsError()
    {
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "value",
                Type = FieldType.UInt8,
                SeekExpression = ExpressionParser.Parse("{-1}"),
            });

        var data = new byte[] { 0x00 };
        var act = () => _decoder.Decode(data, format);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Seek_BeyondDataLength_ThrowsError()
    {
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "value",
                Type = FieldType.UInt8,
                SeekExpression = ExpressionParser.Parse("{100}"),
            });

        var data = new byte[] { 0x00, 0x01, 0x02 };
        var act = () => _decoder.Decode(data, format);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Seek_WithStructField()
    {
        // data: [offset=0x04, 0x00, 0x00, 0x00, 0x00, 0x0A, 0x00, 0x14]
        // seek to 4, read struct with width(u16) + height(u16)
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
                        new FieldDefinition { Name = "ptr", Type = FieldType.UInt8 },
                        new FieldDefinition
                        {
                            Name = "header",
                            Type = FieldType.Struct,
                            StructRef = "inner",
                            SeekExpression = ExpressionParser.Parse("{ptr}"),
                        },
                    ],
                },
                ["inner"] = new()
                {
                    Name = "inner",
                    Fields =
                    [
                        new FieldDefinition { Name = "width", Type = FieldType.UInt16 },
                        new FieldDefinition { Name = "height", Type = FieldType.UInt16 },
                    ],
                },
            },
            RootStruct = "main",
        };

        var data = new byte[] { 0x04, 0x00, 0x00, 0x00, 0x00, 0x0A, 0x00, 0x14 };
        var result = _decoder.Decode(data, format);

        var structNode = result.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        structNode.Offset.Should().Be(4);
        var width = structNode.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        width.Value.Should().Be(10);
        var height = structNode.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        height.Value.Should().Be(20);
    }

    [Fact]
    public void Seek_WithRepeatField()
    {
        // data: [offset=0x02, count=0x03, 0x0A, 0x0B, 0x0C]
        // seek to 2, repeat_count 3, read uint8 x 3
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "offset",
                Type = FieldType.UInt8,
            },
            new FieldDefinition
            {
                Name = "count",
                Type = FieldType.UInt8,
            },
            new FieldDefinition
            {
                Name = "values",
                Type = FieldType.UInt8,
                Repeat = new RepeatMode.Count(ExpressionParser.Parse("{count}")),
                SeekExpression = ExpressionParser.Parse("{offset}"),
            });

        var data = new byte[] { 0x02, 0x03, 0x0A, 0x0B, 0x0C };
        var result = _decoder.Decode(data, format);

        var array = result.Children[2].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);
        array.Elements[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0A);
        array.Elements[1].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0B);
        array.Elements[2].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0C);
    }

    [Fact]
    public void Seek_DoesNotAffectSequentialReading()
    {
        // When no seek is specified, fields read sequentially as before
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "a",
                Type = FieldType.UInt8,
            },
            new FieldDefinition
            {
                Name = "b",
                Type = FieldType.UInt8,
            },
            new FieldDefinition
            {
                Name = "c",
                Type = FieldType.UInt8,
            });

        var data = new byte[] { 0x01, 0x02, 0x03 };
        var result = _decoder.Decode(data, format);

        result.Children.Should().HaveCount(3);
        result.Children[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(1);
        result.Children[1].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(2);
        result.Children[2].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(3);
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
