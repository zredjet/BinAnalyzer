using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class EndiannessOverrideTests
{
    private readonly BinaryDecoder _decoder = new();

    private static FormatDefinition BigEndianFormat(Endianness? structOverride = null) => new()
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
                Endianness = structOverride,
                Fields = [new FieldDefinition { Name = "value", Type = FieldType.UInt16 }],
            },
        },
        RootStruct = "main",
    };

    private static readonly byte[] Data = [0x01, 0x00];

    [Fact]
    public void NoOption_UsesFormatEndianness()
    {
        var root = _decoder.Decode(Data, BigEndianFormat());
        root.Children[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0100);
    }

    [Fact]
    public void Option_OverridesFormatDefault()
    {
        var options = new DecodeOptions { Endianness = Endianness.Little };
        var root = _decoder.Decode(Data, BigEndianFormat(), options);
        root.Children[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0001);
    }

    [Fact]
    public void Option_AppliesToDecodeWithRecovery()
    {
        var options = new DecodeOptions { Endianness = Endianness.Little };
        var result = _decoder.DecodeWithRecovery(Data, BigEndianFormat(), ErrorMode.Continue, options);
        result.Root.Children[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0001);
    }

    [Fact]
    public void StructLevelEndianness_StillWinsOverOption()
    {
        // struct が big を明示 → オプションで little を指定しても struct 指定が優先
        var options = new DecodeOptions { Endianness = Endianness.Little };
        var root = _decoder.Decode(Data, BigEndianFormat(structOverride: Endianness.Big), options);
        root.Children[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(0x0100);
    }
}
