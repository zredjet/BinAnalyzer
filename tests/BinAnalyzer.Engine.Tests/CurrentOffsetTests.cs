using System.IO.Compression;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>
/// REQ-194: 式の <c>_offset</c> は今の読み取り位置（今のデータの先頭から。seek: と同じ基準）。
/// </summary>
public class CurrentOffsetTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void Offset_IsZeroAtTheStartAndAdvancesWithTheFields()
    {
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: at_start
                  type: virtual
                  value: "{_offset}"
                - name: a
                  type: uint8
                - name: b
                  type: uint16
                - name: after_b
                  type: virtual
                  value: "{_offset}"
            """;

        var result = Decode(yaml, [1, 2, 3, 4]);

        Child<DecodedVirtual>(result, "at_start").Value.Should().Be(0L);
        Child<DecodedVirtual>(result, "after_b").Value.Should().Be(3L);
    }

    [Fact]
    public void Offset_InsideSizedScope_IsFromTheStartOfTheFile()
    {
        // remaining はスコープの残り、_offset はファイルの先頭からの位置
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: skip
                  type: bytes
                  size: "2"
                - name: inner
                  type: struct
                  struct: inner
                  size: "4"
              inner:
                - name: x
                  type: uint8
                - name: offset
                  type: virtual
                  value: "{_offset}"
                - name: left
                  type: virtual
                  value: "{remaining}"
            """;

        var inner = Child<DecodedStruct>(Decode(yaml, [0, 0, 1, 2, 3, 4, 5, 6]), "inner");

        Child<DecodedVirtual>(inner, "offset").Value.Should().Be(3L);
        Child<DecodedVirtual>(inner, "left").Value.Should().Be(3L);
    }

    [Fact]
    public void SeekToOffset_RereadsTheSameBytesAsAnotherType()
    {
        // 同じ 2 バイトを uint16 で覗いてから、1 バイトずつ読む
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: pad
                  type: uint8
                - name: peek
                  type: uint16
                  seek: "{_offset}"
                  seek_restore: true
                - name: low
                  type: uint8
                - name: high
                  type: uint8
            """;

        var result = Decode(yaml, [0xFF, 0x34, 0x12]);

        Child<DecodedInteger>(result, "peek").Value.Should().Be(0x1234);
        Child<DecodedInteger>(result, "low").Value.Should().Be(0x34);
        Child<DecodedInteger>(result, "high").Value.Should().Be(0x12);
    }

    [Fact]
    public void Offset_InPerElementSeek_IsThePositionAfterTheSeek()
    {
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: items
                  type: struct
                  struct: item
                  repeat_count: "2"
                  seek: "{4 + _index * 2}"
                  seek_restore: true
              item:
                - name: offset
                  type: virtual
                  value: "{_offset}"
                - name: value
                  type: uint16
            """;

        var items = Child<DecodedArray>(Decode(yaml, [0, 0, 0, 0, 1, 0, 2, 0]), "items").Elements.Cast<DecodedStruct>().ToList();

        items.Select(i => Child<DecodedVirtual>(i, "offset").Value).Should().Equal(4L, 6L);
        items.Select(i => Child<DecodedInteger>(i, "value").Value).Should().Equal(1L, 2L);
    }

    [Fact]
    public void Offset_InsideDecompressedData_IsFromTheStartOfTheContent()
    {
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: pad
                  type: bytes
                  size: "3"
                - name: packed
                  type: zlib
                  size: remaining
                  struct: content
              content:
                - name: a
                  type: uint16
                - name: offset
                  type: virtual
                  value: "{_offset}"
            """;
        var output = new MemoryStream();
        using (var z = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            z.Write([7, 0, 9]);

        var result = Decode(yaml, [0, 0, 0, .. output.ToArray()]);

        var content = ((DecodedCompressed)result.Children.Single(c => c.Name == "packed")).DecodedContent!;
        Child<DecodedVirtual>(content, "offset").Value.Should().Be(2L);
    }

    [Fact]
    public void Offset_InBitstreamMode_IsTheByteBeingRead()
    {
        // 3 ビット読んだ後は 1 バイト目の途中なので 1、8 ビット読み切った後は 2
        const string yaml = """
            name: t
            endianness: big
            root: main
            structs:
              main:
                - name: pad
                  type: uint8
                - name: bits
                  type: struct
                  struct: bits
              bits:
                mode: bitstream
                fields:
                  - name: first
                    type: uint8
                    size: "3"
                  - name: after_first
                    type: virtual
                    value: "{_offset}"
                  - name: rest
                    type: uint8
                    size: "5"
                  - name: after_rest
                    type: virtual
                    value: "{_offset}"
            """;

        var bits = Child<DecodedStruct>(Decode(yaml, [0, 0b1010_0000, 0]), "bits");

        Child<DecodedVirtual>(bits, "after_first").Value.Should().Be(1L);
        Child<DecodedVirtual>(bits, "after_rest").Value.Should().Be(2L);
    }

    private DecodedStruct Decode(string yaml, byte[] data) =>
        _decoder.Decode(data, new YamlFormatLoader().LoadFromString(yaml));

    private static T Child<T>(DecodedStruct parent, string name) where T : DecodedNode =>
        parent.Children.Single(c => c.Name == name).Should().BeOfType<T>().Subject;
}
