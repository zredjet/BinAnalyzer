using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class VirtualFieldTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void VirtualField_DoesNotConsumeBytes()
    {
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "width",
                Type = FieldType.UInt16,
            },
            new FieldDefinition
            {
                Name = "height",
                Type = FieldType.UInt16,
            },
            new FieldDefinition
            {
                Name = "pixel_count",
                Type = FieldType.Virtual,
                ValueExpression = ExpressionParser.Parse("{width * height}"),
            },
            new FieldDefinition
            {
                Name = "after",
                Type = FieldType.UInt8,
            });

        // width=10, height=20 (big-endian), then 0xFF
        var data = new byte[] { 0x00, 0x0A, 0x00, 0x14, 0xFF };
        var result = _decoder.Decode(data, format);

        result.Children.Should().HaveCount(4);

        var virtualNode = result.Children[2].Should().BeOfType<DecodedVirtual>().Subject;
        virtualNode.Name.Should().Be("pixel_count");
        virtualNode.Value.Should().Be(200L);
        virtualNode.Size.Should().Be(0);
        virtualNode.Offset.Should().Be(4); // after width(2) + height(2)

        // The field after virtual should still be at offset 4
        var afterNode = result.Children[3].Should().BeOfType<DecodedInteger>().Subject;
        afterNode.Offset.Should().Be(4);
        afterNode.Value.Should().Be(0xFF);
    }

    [Fact]
    public void VirtualField_ArithmeticExpression()
    {
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
                Name = "sum",
                Type = FieldType.Virtual,
                ValueExpression = ExpressionParser.Parse("{a + b}"),
            });

        var data = new byte[] { 0x03, 0x07 };
        var result = _decoder.Decode(data, format);

        var virtualNode = result.Children[2].Should().BeOfType<DecodedVirtual>().Subject;
        virtualNode.Value.Should().Be(10L);
    }

    [Fact]
    public void VirtualField_FieldReference()
    {
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "value",
                Type = FieldType.UInt32,
            },
            new FieldDefinition
            {
                Name = "doubled",
                Type = FieldType.Virtual,
                ValueExpression = ExpressionParser.Parse("{value * 2}"),
            });

        var data = new byte[] { 0x00, 0x00, 0x00, 0x15 }; // 21
        var result = _decoder.Decode(data, format);

        var virtualNode = result.Children[1].Should().BeOfType<DecodedVirtual>().Subject;
        virtualNode.Value.Should().Be(42L);
    }

    [Fact]
    public void VirtualField_ValueAvailableInSizeExpression()
    {
        // virtual値が後続フィールドのsize式で参照可能なことを確認
        var format = CreateFormat("main",
            new FieldDefinition
            {
                Name = "raw_len",
                Type = FieldType.UInt8,
            },
            new FieldDefinition
            {
                Name = "actual_len",
                Type = FieldType.Virtual,
                ValueExpression = ExpressionParser.Parse("{raw_len - 1}"),
            },
            new FieldDefinition
            {
                Name = "payload",
                Type = FieldType.Bytes,
                SizeExpression = ExpressionParser.Parse("{actual_len}"),
            });

        // raw_len=4, actual_len=3, payload=3 bytes
        var data = new byte[] { 0x04, 0xAA, 0xBB, 0xCC };
        var result = _decoder.Decode(data, format);

        result.Children.Should().HaveCount(3);
        var payload = result.Children[2].Should().BeOfType<DecodedBytes>().Subject;
        payload.Name.Should().Be("payload");
        payload.Size.Should().Be(3);
    }

    [Fact]
    public void VirtualField_ValueAvailableInCondition()
    {
        // virtual値がif条件で参照可能なことを確認
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
                Name = "sum",
                Type = FieldType.Virtual,
                ValueExpression = ExpressionParser.Parse("{a + b}"),
            },
            new FieldDefinition
            {
                Name = "overflow_marker",
                Type = FieldType.UInt8,
                Condition = ExpressionParser.Parse("{sum > 200}"),
            });

        // a=100, b=150 → sum=250 > 200 → overflow_marker included
        var data = new byte[] { 100, 150, 0xFF };
        var result = _decoder.Decode(data, format);

        result.Children.Should().HaveCount(4);
        var marker = result.Children[3].Should().BeOfType<DecodedInteger>().Subject;
        marker.Name.Should().Be("overflow_marker");
        marker.Value.Should().Be(0xFF);
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
