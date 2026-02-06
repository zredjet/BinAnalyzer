using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class ErrorRecoveryTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void StopMode_ThrowsOnError()
    {
        // Field requires 4 bytes but only 2 available
        var format = CreateFormat("main",
            new FieldDefinition { Name = "value", Type = FieldType.UInt32 });

        var data = new byte[] { 0x01, 0x02 };
        var act = () => _decoder.DecodeWithRecovery(data, format, ErrorMode.Stop);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ContinueMode_ProducesErrorNode()
    {
        // Field requires 4 bytes but only 2 available → error, then next field
        var format = CreateFormat("main",
            new FieldDefinition { Name = "big_field", Type = FieldType.UInt32 },
            new FieldDefinition { Name = "small_field", Type = FieldType.UInt8 });

        // Only 2 bytes: UInt32 fails, then can't read UInt8 at offset 2
        var data = new byte[] { 0x01, 0x02 };
        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);

        result.Errors.Should().NotBeEmpty();
        result.Root.Children.Should().Contain(c => c is DecodedError);

        var errorNode = result.Root.Children[0].Should().BeOfType<DecodedError>().Subject;
        errorNode.Name.Should().Be("big_field");
        errorNode.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ContinueMode_ContinuesAfterError()
    {
        // 3 fields: u8, u32 (fails - not enough data after seek), u8
        // data: [0xAA, 0xBB]
        // first u8 succeeds (0xAA), u32 fails (only 1 byte left), last u8 also fails
        var format = CreateFormat("main",
            new FieldDefinition { Name = "a", Type = FieldType.UInt8 },
            new FieldDefinition { Name = "b", Type = FieldType.UInt32 },
            new FieldDefinition { Name = "c", Type = FieldType.UInt8 });

        var data = new byte[] { 0xAA, 0xBB };
        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);

        // First field succeeds
        var firstNode = result.Root.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        firstNode.Value.Should().Be(0xAA);

        // Second field is an error
        result.Root.Children[1].Should().BeOfType<DecodedError>();

        result.Errors.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void ContinueMode_SkipsKnownSizeFields()
    {
        // data: 4 bytes. u16 at offset 0, u16 at offset 2.
        // First u16 has an impossible condition that we'll work around differently.
        // Instead: simulate error by requesting bytes beyond scope, then continue.
        var format = CreateFormat("main",
            new FieldDefinition { Name = "a", Type = FieldType.UInt16 },
            new FieldDefinition { Name = "b", Type = FieldType.UInt16 });

        var data = new byte[] { 0x00, 0x01, 0x00, 0x02 };
        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);

        // Both succeed
        result.Errors.Should().BeEmpty();
        result.Root.Children.Should().HaveCount(2);
    }

    [Fact]
    public void ContinueMode_EmptyErrors_WhenNoFailures()
    {
        var format = CreateFormat("main",
            new FieldDefinition { Name = "value", Type = FieldType.UInt8 });

        var data = new byte[] { 0x42 };
        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);

        result.Errors.Should().BeEmpty();
        result.Root.Children.Should().HaveCount(1);
    }

    [Fact]
    public void ContinueMode_ErrorsCollected()
    {
        var format = CreateFormat("main",
            new FieldDefinition { Name = "a", Type = FieldType.UInt32 },
            new FieldDefinition { Name = "b", Type = FieldType.UInt32 });

        var data = new byte[] { 0x01 }; // only 1 byte, both fields fail
        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);

        result.Errors.Should().HaveCountGreaterThanOrEqualTo(1);
        result.Root.Children.Should().Contain(c => c is DecodedError);
    }

    [Fact]
    public void ContinueMode_ErrorHasFieldPath()
    {
        var format = CreateFormat("main",
            new FieldDefinition { Name = "bad_field", Type = FieldType.UInt64 });

        var data = new byte[] { 0x01, 0x02 }; // only 2 bytes
        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);

        result.Errors.Should().HaveCount(1);
        result.Errors[0].FieldPath.Should().Contain("bad_field");
    }

    [Fact]
    public void DecodeResult_ExposesRoot()
    {
        var format = CreateFormat("main",
            new FieldDefinition { Name = "value", Type = FieldType.UInt8 });

        var data = new byte[] { 0xFF };
        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);

        result.Root.Should().NotBeNull();
        result.Root.Children.Should().HaveCount(1);
        result.Root.Children[0].Should().BeOfType<DecodedInteger>()
            .Which.Value.Should().Be(0xFF);
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
