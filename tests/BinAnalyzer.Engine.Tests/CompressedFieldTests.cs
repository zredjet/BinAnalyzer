using System.IO.Compression;
using BinAnalyzer.Core;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class CompressedFieldTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void Decode_Zlib_DecompressesData()
    {
        var original = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var compressed = ZlibCompress(original);

        var format = CreateFormat("main", new FieldDefinition
        {
            Name = "data",
            Type = FieldType.Zlib,
            Size = compressed.Length,
        });

        var result = _decoder.Decode(compressed, format);

        var field = result.Children[0].Should().BeOfType<DecodedCompressed>().Subject;
        field.Algorithm.Should().Be("zlib");
        field.CompressedSize.Should().Be(compressed.Length);
        field.DecompressedSize.Should().Be(5);
        field.RawDecompressed.Should().NotBeNull();
        field.RawDecompressed!.Value.ToArray().Should().BeEquivalentTo(original);
    }

    [Fact]
    public void Decode_Deflate_DecompressesData()
    {
        var original = new byte[] { 0xAA, 0xBB, 0xCC };
        var compressed = DeflateCompress(original);

        var format = CreateFormat("main", new FieldDefinition
        {
            Name = "data",
            Type = FieldType.Deflate,
            Size = compressed.Length,
        });

        var result = _decoder.Decode(compressed, format);

        var field = result.Children[0].Should().BeOfType<DecodedCompressed>().Subject;
        field.Algorithm.Should().Be("deflate");
        field.DecompressedSize.Should().Be(3);
        field.RawDecompressed!.Value.ToArray().Should().BeEquivalentTo(original);
    }

    [Fact]
    public void Decode_Zlib_WithStructRef_DecodesContent()
    {
        var original = new byte[] { 0x00, 0x00, 0x00, 0x2A, 0x00, 0x0D }; // uint32=42, uint16=13
        var compressed = ZlibCompress(original);

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
                    Fields = [new FieldDefinition
                    {
                        Name = "compressed",
                        Type = FieldType.Zlib,
                        Size = compressed.Length,
                        StructRef = "inner",
                    }],
                },
                ["inner"] = new()
                {
                    Name = "inner",
                    Fields =
                    [
                        new FieldDefinition { Name = "value", Type = FieldType.UInt32 },
                        new FieldDefinition { Name = "tag", Type = FieldType.UInt16 },
                    ],
                },
            },
            RootStruct = "main",
        };

        var result = _decoder.Decode(compressed, format);

        var field = result.Children[0].Should().BeOfType<DecodedCompressed>().Subject;
        field.DecodedContent.Should().NotBeNull();
        field.RawDecompressed.Should().BeNull();

        var inner = field.DecodedContent!;
        inner.Children.Should().HaveCount(2);
        inner.Children[0].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(42);
        inner.Children[1].Should().BeOfType<DecodedInteger>().Which.Value.Should().Be(13);
    }

    [Fact]
    public void Decode_Zlib_DynamicSize()
    {
        var original = new byte[] { 0x01, 0x02, 0x03 };
        var compressed = ZlibCompress(original);

        var format = CreateFormat("main",
            new FieldDefinition { Name = "length", Type = FieldType.UInt16 },
            new FieldDefinition
            {
                Name = "data",
                Type = FieldType.Zlib,
                SizeExpression = ExpressionParser.Parse("{length}"),
            });

        // Build data: uint16 length (BE) + compressed data
        var data = new byte[2 + compressed.Length];
        data[0] = (byte)(compressed.Length >> 8);
        data[1] = (byte)(compressed.Length & 0xFF);
        compressed.CopyTo(data, 2);

        var result = _decoder.Decode(data, format);

        result.Children.Should().HaveCount(2);
        var compField = result.Children[1].Should().BeOfType<DecodedCompressed>().Subject;
        compField.DecompressedSize.Should().Be(3);
    }

    [Fact]
    public void Decode_InvalidZlib_ThrowsDecodeException()
    {
        var format = CreateFormat("main", new FieldDefinition
        {
            Name = "data",
            Type = FieldType.Zlib,
            Size = 4,
        });
        var data = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }; // invalid zlib

        var act = () => _decoder.Decode(data, format);

        act.Should().Throw<DecodeException>().Where(e => e.Message.Contains("decompress"));
    }

    [Fact]
    public void Decode_Zlib_DecompressedSizeReported()
    {
        var original = new byte[100];
        for (var i = 0; i < original.Length; i++)
            original[i] = (byte)(i % 256);
        var compressed = ZlibCompress(original);

        var format = CreateFormat("main", new FieldDefinition
        {
            Name = "data",
            Type = FieldType.Zlib,
            Size = compressed.Length,
        });

        var result = _decoder.Decode(compressed, format);

        var field = result.Children[0].Should().BeOfType<DecodedCompressed>().Subject;
        field.CompressedSize.Should().Be(compressed.Length);
        field.DecompressedSize.Should().Be(100);
    }

    private static byte[] ZlibCompress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var compressor = new ZLibStream(output, CompressionLevel.Optimal))
        {
            compressor.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private static byte[] DeflateCompress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var compressor = new DeflateStream(output, CompressionLevel.Optimal))
        {
            compressor.Write(data, 0, data.Length);
        }
        return output.ToArray();
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
