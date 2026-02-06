using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class AlignmentTests
{
    // --- DecodeContext.AlignTo ---

    [Fact]
    public void AlignTo_AlreadyAligned_NoChange()
    {
        var data = new byte[16];
        var ctx = new DecodeContext(data, Endianness.Big);

        var skipped = ctx.AlignTo(4);

        skipped.Should().Be(0);
        ctx.Position.Should().Be(0);
    }

    [Fact]
    public void AlignTo_AdvancesToBoundary()
    {
        var data = new byte[16];
        var ctx = new DecodeContext(data, Endianness.Big);
        ctx.ReadUInt8(); // position = 1

        var skipped = ctx.AlignTo(4);

        skipped.Should().Be(3);
        ctx.Position.Should().Be(4);
    }

    [Fact]
    public void AlignTo_Position2_Align4()
    {
        var data = new byte[16];
        var ctx = new DecodeContext(data, Endianness.Big);
        ctx.ReadUInt16(); // position = 2

        var skipped = ctx.AlignTo(4);

        skipped.Should().Be(2);
        ctx.Position.Should().Be(4);
    }

    [Fact]
    public void AlignTo_Position3_Align4()
    {
        var data = new byte[16];
        var ctx = new DecodeContext(data, Endianness.Big);
        ctx.ReadUInt8();
        ctx.ReadUInt8();
        ctx.ReadUInt8(); // position = 3

        var skipped = ctx.AlignTo(4);

        skipped.Should().Be(1);
        ctx.Position.Should().Be(4);
    }

    [Fact]
    public void AlignTo_Position4_Align4_NoChange()
    {
        var data = new byte[16];
        var ctx = new DecodeContext(data, Endianness.Big);
        ctx.ReadUInt32(); // position = 4

        var skipped = ctx.AlignTo(4);

        skipped.Should().Be(0);
        ctx.Position.Should().Be(4);
    }

    [Fact]
    public void AlignTo_Align1_NeverPads()
    {
        var data = new byte[16];
        var ctx = new DecodeContext(data, Endianness.Big);
        ctx.ReadUInt8(); // position = 1

        var skipped = ctx.AlignTo(1);

        skipped.Should().Be(0);
        ctx.Position.Should().Be(1);
    }

    [Fact]
    public void AlignTo_InsufficientBytes_Throws()
    {
        var data = new byte[3];
        var ctx = new DecodeContext(data, Endianness.Big);
        ctx.PushScope(3);
        ctx.ReadUInt8(); // position = 1, remaining = 2 in scope

        var act = () => ctx.AlignTo(4); // needs 3 bytes padding, only 2 remaining
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AlignTo_InvalidAlignment_Throws()
    {
        var data = new byte[16];
        var ctx = new DecodeContext(data, Endianness.Big);

        var act = () => ctx.AlignTo(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AlignTo_NegativeAlignment_Throws()
    {
        var data = new byte[16];
        var ctx = new DecodeContext(data, Endianness.Big);

        var act = () => ctx.AlignTo(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // --- Field-level alignment via BinaryDecoder ---

    [Fact]
    public void FieldAlign_PadsNextFieldStart()
    {
        // Layout: [u8] [pad:3] [u32]
        // Position: 0     1      4
        var data = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0A };
        var format = new FormatDefinition
        {
            Name = "test",
            RootStruct = "root",
            Structs = new Dictionary<string, StructDefinition>
            {
                ["root"] = new()
                {
                    Name = "root",
                    Fields =
                    [
                        new FieldDefinition { Name = "tag", Type = FieldType.UInt8, Align = 4 },
                        new FieldDefinition { Name = "value", Type = FieldType.UInt32 },
                    ],
                },
            },
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
        };

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var tag = result.Children.First(c => c.Name == "tag") as Core.Decoded.DecodedInteger;
        var value = result.Children.First(c => c.Name == "value") as Core.Decoded.DecodedInteger;

        tag.Should().NotBeNull();
        tag!.Value.Should().Be(1);
        tag.Offset.Should().Be(0);

        value.Should().NotBeNull();
        value!.Value.Should().Be(10);
        value!.Offset.Should().Be(4);
    }

    // --- IsPadding flag ---

    [Fact]
    public void PaddingField_HasIsPaddingTrue()
    {
        var data = new byte[] { 0x42, 0x00, 0x00, 0x00 };
        var format = new FormatDefinition
        {
            Name = "test",
            RootStruct = "root",
            Structs = new Dictionary<string, StructDefinition>
            {
                ["root"] = new()
                {
                    Name = "root",
                    Fields =
                    [
                        new FieldDefinition { Name = "value", Type = FieldType.UInt8 },
                        new FieldDefinition { Name = "pad", Type = FieldType.Bytes, Size = 3, IsPadding = true },
                    ],
                },
            },
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
        };

        var decoder = new BinaryDecoder();
        var result = decoder.Decode(data, format);

        var pad = result.Children.First(c => c.Name == "pad");
        pad.IsPadding.Should().BeTrue();
    }

}
