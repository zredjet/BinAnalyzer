using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class EndiannessTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void StructLevel_OverridesFormatEndianness()
    {
        // Format is big-endian, struct overrides to little-endian
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
                        new FieldDefinition
                        {
                            Name = "header",
                            Type = FieldType.Struct,
                            StructRef = "le_struct",
                        },
                    ],
                },
                ["le_struct"] = new()
                {
                    Name = "le_struct",
                    Endianness = Endianness.Little,
                    Fields =
                    [
                        new FieldDefinition { Name = "value", Type = FieldType.UInt16 },
                    ],
                },
            },
            RootStruct = "main",
        };

        // 0x01 0x00 in little-endian = 1
        var data = new byte[] { 0x01, 0x00 };
        var result = _decoder.Decode(data, format);

        var structNode = result.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        var intNode = structNode.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        intNode.Value.Should().Be(1); // LE: 0x0001
    }

    [Fact]
    public void FieldLevel_OverridesStructEndianness()
    {
        // Struct is big-endian, field overrides to little-endian
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
                        new FieldDefinition
                        {
                            Name = "be_value",
                            Type = FieldType.UInt16,
                        },
                        new FieldDefinition
                        {
                            Name = "le_value",
                            Type = FieldType.UInt16,
                            Endianness = Endianness.Little,
                        },
                    ],
                },
            },
            RootStruct = "main",
        };

        // bytes: 0x00 0x01 | 0x01 0x00
        var data = new byte[] { 0x00, 0x01, 0x01, 0x00 };
        var result = _decoder.Decode(data, format);

        var beNode = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        beNode.Value.Should().Be(1); // BE: 0x0001

        var leNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        leNode.Value.Should().Be(1); // LE: 0x0001
    }

    [Fact]
    public void Priority_FieldOverridesStruct()
    {
        // Struct is little-endian, field overrides back to big-endian
        var format = new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Little,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["main"] = new()
                {
                    Name = "main",
                    Endianness = Endianness.Little,
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "le_value",
                            Type = FieldType.UInt16,
                        },
                        new FieldDefinition
                        {
                            Name = "be_value",
                            Type = FieldType.UInt16,
                            Endianness = Endianness.Big,
                        },
                    ],
                },
            },
            RootStruct = "main",
        };

        var data = new byte[] { 0x01, 0x00, 0x00, 0x01 };
        var result = _decoder.Decode(data, format);

        var leNode = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        leNode.Value.Should().Be(1); // LE: 0x0001

        var beNode = result.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        beNode.Value.Should().Be(1); // BE: 0x0001
    }

    [Fact]
    public void NestedStruct_InheritsParentEndianness()
    {
        // Parent struct is little-endian, nested struct has no override → inherits
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
                    Endianness = Endianness.Little,
                    Fields =
                    [
                        new FieldDefinition
                        {
                            Name = "inner",
                            Type = FieldType.Struct,
                            StructRef = "nested",
                        },
                    ],
                },
                ["nested"] = new()
                {
                    Name = "nested",
                    // No endianness override
                    Fields =
                    [
                        new FieldDefinition { Name = "value", Type = FieldType.UInt16 },
                    ],
                },
            },
            RootStruct = "main",
        };

        // 0x01 0x00 in little-endian = 1
        var data = new byte[] { 0x01, 0x00 };
        var result = _decoder.Decode(data, format);

        var structNode = result.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        var intNode = structNode.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        intNode.Value.Should().Be(1); // inherits LE from parent
    }

    [Fact]
    public void NoOverride_UsesFormatEndianness()
    {
        // No struct or field override → uses format default
        var format = new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Little,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["main"] = new()
                {
                    Name = "main",
                    Fields =
                    [
                        new FieldDefinition { Name = "value", Type = FieldType.UInt16 },
                    ],
                },
            },
            RootStruct = "main",
        };

        var data = new byte[] { 0x01, 0x00 };
        var result = _decoder.Decode(data, format);

        var node = result.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        node.Value.Should().Be(1); // LE: 0x0001
    }
}
