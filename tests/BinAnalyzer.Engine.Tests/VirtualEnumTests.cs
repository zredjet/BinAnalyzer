using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>REQ-186: virtual の enum。整数の結果にだけラベルを付ける。</summary>
public class VirtualEnumTests
{
    private readonly BinaryDecoder _decoder = new();

    private static FormatDefinition Format(string expression) => new()
    {
        Name = "Test",
        Endianness = Endianness.Big,
        Enums = new Dictionary<string, EnumDefinition>
        {
            ["os"] = new() { Name = "os", Entries = [new EnumEntry(0, "MS-DOS"), new EnumEntry(3, "unix", "UNIX 系")] },
        },
        Flags = new Dictionary<string, FlagsDefinition>(),
        Structs = new Dictionary<string, StructDefinition>
        {
            ["main"] = new()
            {
                Name = "main",
                Fields =
                [
                    new FieldDefinition { Name = "raw", Type = FieldType.UInt16 },
                    new FieldDefinition { Name = "v", Type = FieldType.Virtual, EnumRef = "os", ValueExpression = ExpressionParser.Parse(expression) },
                ],
            },
        },
        RootStruct = "main",
    };

    private DecodedVirtual DecodeVirtual(string expression, byte[] data) =>
        _decoder.Decode(data, Format(expression)).Children[1].Should().BeOfType<DecodedVirtual>().Subject;

    [Fact]
    public void IntegerResult_GetsEnumLabelAndDescription()
    {
        var v = DecodeVirtual("{raw >> 8}", [0x03, 0x14]);

        v.Value.Should().Be(3L);
        v.EnumLabel.Should().Be("unix");
        v.EnumDescription.Should().Be("UNIX 系");
    }

    [Fact]
    public void IntegerWithoutMatchingEntry_NoLabel()
    {
        DecodeVirtual("{raw >> 8}", [0x07, 0x00]).EnumLabel.Should().BeNull();
    }

    [Fact]
    public void BooleanResult_NoLabel()
    {
        // 真偽値は 0 / 1 のエントリがあってもラベルを付けない
        DecodeVirtual("{raw == 0}", [0x00, 0x00]).EnumLabel.Should().BeNull();
    }
}
