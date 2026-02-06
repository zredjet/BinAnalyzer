using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class DecodeContextTests
{
    [Fact]
    public void ReadUInt8_ReadsCorrectly()
    {
        var data = new byte[] { 0x42 };
        var ctx = new DecodeContext(data, Endianness.Big);

        ctx.ReadUInt8().Should().Be(0x42);
        ctx.Position.Should().Be(1);
        ctx.IsEof.Should().BeTrue();
    }

    [Fact]
    public void ReadUInt32_BigEndian()
    {
        var data = new byte[] { 0x00, 0x00, 0x00, 0x0D };
        var ctx = new DecodeContext(data, Endianness.Big);

        ctx.ReadUInt32().Should().Be(13u);
    }

    [Fact]
    public void ReadUInt32_LittleEndian()
    {
        var data = new byte[] { 0x0D, 0x00, 0x00, 0x00 };
        var ctx = new DecodeContext(data, Endianness.Little);

        ctx.ReadUInt32().Should().Be(13u);
    }

    [Fact]
    public void ReadAscii_ReadsCorrectly()
    {
        var data = "IHDR"u8.ToArray();
        var ctx = new DecodeContext(data, Endianness.Big);

        ctx.ReadAscii(4).Should().Be("IHDR");
    }

    [Fact]
    public void ReadBytes_ReadsCorrectly()
    {
        var data = new byte[] { 0x01, 0x02, 0x03 };
        var ctx = new DecodeContext(data, Endianness.Big);

        var result = ctx.ReadBytes(3);
        result.ToArray().Should().BeEquivalentTo(new byte[] { 0x01, 0x02, 0x03 });
    }

    [Fact]
    public void PushScope_LimitsReading()
    {
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var ctx = new DecodeContext(data, Endianness.Big);

        ctx.PushScope(2);
        ctx.Remaining.Should().Be(2);
        ctx.ReadUInt8().Should().Be(0x01);
        ctx.Remaining.Should().Be(1);
    }

    [Fact]
    public void PopScope_AdvancesToEndOfScope()
    {
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var ctx = new DecodeContext(data, Endianness.Big);

        ctx.PushScope(2);
        ctx.ReadUInt8(); // read 1 byte
        ctx.PopScope();  // should advance to position 2

        ctx.Position.Should().Be(2);
        ctx.ReadUInt8().Should().Be(0x03);
    }

    [Fact]
    public void Variables_ScopeResolution()
    {
        var data = new byte[] { 0x00 };
        var ctx = new DecodeContext(data, Endianness.Big);

        ctx.SetVariable("outer", 42L);
        ctx.PushScope(1);
        ctx.SetVariable("inner", 99L);

        // Inner scope sees both
        ctx.GetVariable("inner").Should().Be(99L);
        ctx.GetVariable("outer").Should().Be(42L);

        ctx.PopScope();

        // Outer scope sees only outer
        ctx.GetVariable("outer").Should().Be(42L);
        ctx.GetVariable("inner").Should().BeNull();
    }

    [Fact]
    public void ReadBeyondScope_Throws()
    {
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var ctx = new DecodeContext(data, Endianness.Big);

        ctx.PushScope(1);
        ctx.ReadUInt8();
        var act = () => ctx.ReadUInt8();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReadInt16_BigEndian()
    {
        var data = new byte[] { 0xFF, 0xFE }; // -2 in big-endian signed
        var ctx = new DecodeContext(data, Endianness.Big);
        ctx.ReadInt16().Should().Be(-2);
    }
}
