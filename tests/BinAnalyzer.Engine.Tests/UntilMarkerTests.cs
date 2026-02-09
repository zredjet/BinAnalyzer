using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class UntilMarkerTests
{
    [Fact]
    public void UntilMarker_TwoByteMarker_InMiddle_ReturnsCorrectOffset()
    {
        // Data: [0x01, 0x02, 0x03, 0xFF, 0xD9, 0x00]
        // Position: 0, marker at index 3 → distance = 3
        var data = new byte[] { 0x01, 0x02, 0x03, 0xFF, 0xD9, 0x00 };
        var ctx = new DecodeContext(data, Endianness.Big);

        var expr = ExpressionParser.Parse("{until_marker(0xFF, 0xD9)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(3);
    }

    [Fact]
    public void UntilMarker_MarkerAtStart_ReturnsZero()
    {
        // Data: [0xFF, 0xD9, 0x01, 0x02]
        // Position: 0, marker at index 0 → distance = 0
        var data = new byte[] { 0xFF, 0xD9, 0x01, 0x02 };
        var ctx = new DecodeContext(data, Endianness.Big);

        var expr = ExpressionParser.Parse("{until_marker(0xFF, 0xD9)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(0);
    }

    [Fact]
    public void UntilMarker_MarkerNotFound_ReturnsRemaining()
    {
        // Data: [0x01, 0x02, 0x03, 0x04]
        // No 0xFF 0xD9 → returns remaining (4)
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var ctx = new DecodeContext(data, Endianness.Big);

        var expr = ExpressionParser.Parse("{until_marker(0xFF, 0xD9)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(4);
    }

    [Fact]
    public void UntilMarker_MarkerAtEnd_ReturnsCorrectDistance()
    {
        // Data: [0x01, 0x02, 0xFF, 0xD9]
        // Position: 0, marker at index 2 → distance = 2
        var data = new byte[] { 0x01, 0x02, 0xFF, 0xD9 };
        var ctx = new DecodeContext(data, Endianness.Big);

        var expr = ExpressionParser.Parse("{until_marker(0xFF, 0xD9)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(2);
    }

    [Fact]
    public void UntilMarker_NoArguments_ThrowsError()
    {
        var data = new byte[] { 0x01 };
        var ctx = new DecodeContext(data, Endianness.Big);

        var expr = ExpressionParser.Parse("{until_marker()}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least 1 argument*");
    }

    [Fact]
    public void UntilMarker_SingleByteMarker_Works()
    {
        // Data: [0x01, 0x02, 0xFF, 0x03]
        // Search for single byte 0xFF → found at index 2
        var data = new byte[] { 0x01, 0x02, 0xFF, 0x03 };
        var ctx = new DecodeContext(data, Endianness.Big);

        var expr = ExpressionParser.Parse("{until_marker(0xFF)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(2);
    }

    [Fact]
    public void UntilMarker_AfterReadingBytes_ReturnsOffsetFromCurrentPosition()
    {
        // Data: [0xAA, 0xBB, 0x01, 0x02, 0xFF, 0xD9]
        // Read 2 bytes → position = 2, marker at index 4 → distance = 2
        var data = new byte[] { 0xAA, 0xBB, 0x01, 0x02, 0xFF, 0xD9 };
        var ctx = new DecodeContext(data, Endianness.Big);
        ctx.ReadUInt8();
        ctx.ReadUInt8();

        var expr = ExpressionParser.Parse("{until_marker(0xFF, 0xD9)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(2);
    }

    [Fact]
    public void UntilMarker_WithinScope_OnlySearchesScope()
    {
        // Data: [0x01, 0x02, 0x03, 0xFF, 0xD9, 0x04, 0x05]
        // Push scope of size 3 → scope covers [0x01, 0x02, 0x03]
        // Marker is outside scope → returns remaining (3)
        var data = new byte[] { 0x01, 0x02, 0x03, 0xFF, 0xD9, 0x04, 0x05 };
        var ctx = new DecodeContext(data, Endianness.Big);
        ctx.PushScope(3);

        var expr = ExpressionParser.Parse("{until_marker(0xFF, 0xD9)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(3);
    }
}
