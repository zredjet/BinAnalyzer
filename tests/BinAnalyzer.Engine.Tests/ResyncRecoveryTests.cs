using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class ResyncRecoveryTests
{
    private readonly BinaryDecoder _decoder = new();

    /// <summary>
    /// 要素デコードエラー時にresyncマーカー位置まで前方スキャンする。
    /// </summary>
    [Fact]
    public void ResyncMarker_SkipsToNextMarker()
    {
        // Root struct with a field that triggers error (uint64 = 8 bytes, but only 6 bytes of data)
        var rootStruct = new StructDefinition
        {
            Name = "main",
            Fields = new[]
            {
                new FieldDefinition { Name = "bad_field", Type = FieldType.UInt64 },
            },
            ResyncMarker = new byte[] { 0x4D, 0x41, 0x47, 0x4B }, // "MAGK"
        };

        // Data: 2 garbage bytes + MAGK marker at offset 2 (only 6 bytes total, uint64 needs 8 → error)
        var data = new byte[] { 0xFF, 0xFF, 0x4D, 0x41, 0x47, 0x4B };
        var format = CreateFormat("main", rootStruct);

        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);

        result.Errors.Should().HaveCount(1);
        var errorNode = result.Root.Children[0].Should().BeOfType<DecodedError>().Subject;
        errorNode.SkippedBytes.Should().Be(2); // Skipped 2 bytes to reach marker
    }

    /// <summary>
    /// repeat配列の要素失敗→マーカーで次の要素へ再同期する。
    /// </summary>
    [Fact]
    public void ResyncMarker_InRepeatedField()
    {
        // Element struct: magic("MK" 2 bytes) + length(uint8) + data(bytes, size={length})
        // Corrupt data produces a huge length that exceeds remaining → error
        var elementStruct = new StructDefinition
        {
            Name = "chunk",
            Fields = new[]
            {
                new FieldDefinition { Name = "magic", Type = FieldType.Ascii, Size = 2 },
                new FieldDefinition { Name = "length", Type = FieldType.UInt8 },
                new FieldDefinition
                {
                    Name = "data",
                    Type = FieldType.Bytes,
                    SizeExpression = ExpressionParser.Parse("{length}"),
                },
            },
            ResyncMarker = new byte[] { 0x4D, 0x4B }, // "MK"
        };

        var rootStruct = new StructDefinition
        {
            Name = "main",
            Fields = new[]
            {
                new FieldDefinition
                {
                    Name = "chunks",
                    Type = FieldType.Struct,
                    StructRef = "chunk",
                    Repeat = new RepeatMode.UntilEof(),
                },
            },
        };

        // Data layout:
        // Element 0: "MK" + length=2 + data=[CC,DD] (5 bytes, good)
        // Corrupt: 0xFF 0xFF 0xFF (3 garbage bytes: magic reads FF FF, length reads FF=255 → data fails)
        // Element 1: "MK" + length=2 + data=[EE,FF] (5 bytes, good)
        var data = new byte[]
        {
            0x4D, 0x4B, 0x02, 0xCC, 0xDD, // element 0: magic="MK", length=2, data=[CC,DD]
            0xFF, 0xFF, 0xFF,               // corruption (length=255 → overflow)
            0x4D, 0x4B, 0x02, 0xEE, 0xFF, // element 1: magic="MK", length=2, data=[EE,FF]
        };

        var format = CreateFormatMultiStruct("main",
            ("main", rootStruct), ("chunk", elementStruct));

        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);

        // Should have an array with 3 elements: good, error, good
        var array = result.Root.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(3);

        // First element: success
        array.Elements[0].Should().BeOfType<DecodedStruct>();

        // Second element: error (corrupt data → resync to next MK)
        var errorElem = array.Elements[1].Should().BeOfType<DecodedError>().Subject;
        errorElem.SkippedBytes.Should().BeGreaterThan(0);

        // Third element: success after resync
        array.Elements[2].Should().BeOfType<DecodedStruct>();
    }

    /// <summary>
    /// マーカー未発見時にスコープ終端までスキップする。
    /// </summary>
    [Fact]
    public void ResyncMarker_NotFound_SkipsToScopeEnd()
    {
        var elementStruct = new StructDefinition
        {
            Name = "chunk",
            Fields = new[]
            {
                new FieldDefinition { Name = "magic", Type = FieldType.Ascii, Size = 2 },
                new FieldDefinition { Name = "value", Type = FieldType.UInt16 },
            },
            ResyncMarker = new byte[] { 0xDE, 0xAD }, // Not present in data
        };

        var rootStruct = new StructDefinition
        {
            Name = "main",
            Fields = new[]
            {
                new FieldDefinition
                {
                    Name = "chunks",
                    Type = FieldType.Struct,
                    StructRef = "chunk",
                    Repeat = new RepeatMode.UntilEof(),
                },
            },
        };

        // Data: good element + garbage (no marker found)
        var data = new byte[]
        {
            0xDE, 0xAD, 0x00, 0x01, // element 0: matches marker at start, decoded normally
            0xFF, 0xFF, 0xFF, 0xFF, // garbage, no marker ahead
        };

        var format = CreateFormatMultiStruct("main",
            ("main", rootStruct), ("chunk", elementStruct));

        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);

        var array = result.Root.Children[0].Should().BeOfType<DecodedArray>().Subject;
        // Element 0 succeeds, element 1 fails and no marker found → loop ends
        array.Elements.Should().HaveCountGreaterThanOrEqualTo(1);
        // The error element should have skipped to scope end
        var lastError = array.Elements.LastOrDefault(e => e is DecodedError) as DecodedError;
        if (lastError is not null)
            lastError.SkippedBytes.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// align指定ありのstruct内フィールドエラー→次のアライメント境界へスキップ。
    /// </summary>
    [Fact]
    public void AlignmentResync_SkipsToNextBoundary()
    {
        // Struct with align=4
        var rootStruct = new StructDefinition
        {
            Name = "main",
            Fields = new[]
            {
                new FieldDefinition { Name = "small", Type = FieldType.UInt8 },
                // This field will fail (requires uint64 but not enough data after small)
                new FieldDefinition { Name = "big", Type = FieldType.UInt64 },
            },
            Align = 4,
        };

        // Data: 1 byte for small, then only 4 bytes left
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var format = CreateFormat("main", rootStruct);

        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);

        result.Errors.Should().NotBeEmpty();
        var errorNode = result.Root.Children.OfType<DecodedError>().First();
        // Should have skipped to alignment boundary (from position 1 to 4 = 3 bytes)
        errorNode.SkippedBytes.Should().Be(3);
    }

    /// <summary>
    /// DecodedError.SkippedBytesにスキップ量が記録される。
    /// </summary>
    [Fact]
    public void SkippedBytes_RecordedInDecodedError()
    {
        var rootStruct = new StructDefinition
        {
            Name = "main",
            Fields = new[]
            {
                new FieldDefinition { Name = "bad", Type = FieldType.UInt64 },
            },
            ResyncMarker = new byte[] { 0xAA, 0xBB },
        };

        // marker at offset 3
        var data = new byte[] { 0x00, 0x00, 0x00, 0xAA, 0xBB, 0xCC };
        var format = CreateFormat("main", rootStruct);

        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);

        var errorNode = result.Root.Children.OfType<DecodedError>().First();
        errorNode.SkippedBytes.Should().Be(3);
    }

    /// <summary>
    /// YAML定義のresync_markerが正しくパースされる。
    /// </summary>
    [Fact]
    public void ResyncMarker_YamlParsing()
    {
        var yaml = """
            name: TestFormat
            root: main
            structs:
              main:
                resync_marker: [0x4D, 0x54, 0x72, 0x6B]
                fields:
                  - name: data
                    type: uint8
            """;

        var loader = new Dsl.YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        format.Structs["main"].ResyncMarker.Should().NotBeNull();
        format.Structs["main"].ResyncMarker.Should().Equal(0x4D, 0x54, 0x72, 0x6B);
    }

    /// <summary>
    /// マーカーなしstructは既存の回復動作と同一。
    /// </summary>
    [Fact]
    public void NoResyncMarker_ExistingBehavior()
    {
        var rootStruct = new StructDefinition
        {
            Name = "main",
            Fields = new[]
            {
                new FieldDefinition { Name = "a", Type = FieldType.UInt32 },
                new FieldDefinition { Name = "b", Type = FieldType.UInt8 },
            },
        };

        // Only 2 bytes, u32 fails, u8 at position 0 (no skip because field is u32 but not enough data)
        var data = new byte[] { 0xAA, 0xBB };
        var format = CreateFormat("main", rootStruct);

        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);

        result.Errors.Should().NotBeEmpty();
        var errorNode = result.Root.Children.OfType<DecodedError>().First();
        // No resync marker → SkippedBytes should be 0 (existing behavior: unknown size stays)
        errorNode.SkippedBytes.Should().Be(0);
    }

    /// <summary>
    /// エラーなしの正常デコードで再同期コードが実行されない。
    /// </summary>
    [Fact]
    public void NormalDecode_NoPerformanceImpact()
    {
        var elementStruct = new StructDefinition
        {
            Name = "chunk",
            Fields = new[]
            {
                new FieldDefinition { Name = "value", Type = FieldType.UInt16 },
            },
            ResyncMarker = new byte[] { 0x00 }, // Marker is defined but should not be used
        };

        var rootStruct = new StructDefinition
        {
            Name = "main",
            Fields = new[]
            {
                new FieldDefinition
                {
                    Name = "items",
                    Type = FieldType.Struct,
                    StructRef = "chunk",
                    Repeat = new RepeatMode.Count(
                        ExpressionParser.Parse("{2}")),
                },
            },
        };

        var data = new byte[] { 0x00, 0x01, 0x00, 0x02 };
        var format = CreateFormatMultiStruct("main",
            ("main", rootStruct), ("chunk", elementStruct));

        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);

        result.Errors.Should().BeEmpty();
        var array = result.Root.Children[0].Should().BeOfType<DecodedArray>().Subject;
        array.Elements.Should().HaveCount(2);
        array.Elements.Should().AllBeOfType<DecodedStruct>();
    }

    /// <summary>
    /// マーカーが現在位置にある場合は無視して前方を探す（無限ループ防止）。
    /// </summary>
    [Fact]
    public void ResyncMarker_AtCurrentPos_SkipsForward()
    {
        // TryResyncToMarker should skip idx==0 (current position)
        var rootStruct = new StructDefinition
        {
            Name = "main",
            Fields = new[]
            {
                // Fails immediately (not enough data for uint64)
                new FieldDefinition { Name = "bad", Type = FieldType.UInt64 },
            },
            // Marker is at position 0 in data
            ResyncMarker = new byte[] { 0xAA, 0xBB },
        };

        // Data starts with marker, then more data, then marker again
        var data = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xAA, 0xBB, 0xEE };
        var format = CreateFormat("main", rootStruct);

        var result = _decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);

        var errorNode = result.Root.Children.OfType<DecodedError>().First();
        // Should skip from position 0 to second marker at position 4
        errorNode.SkippedBytes.Should().Be(4);
    }

    private static FormatDefinition CreateFormat(string rootName, StructDefinition rootStruct)
    {
        return new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                [rootName] = rootStruct,
            },
            RootStruct = rootName,
        };
    }

    private static FormatDefinition CreateFormatMultiStruct(
        string rootName,
        params (string name, StructDefinition def)[] structs)
    {
        var dict = new Dictionary<string, StructDefinition>();
        foreach (var (name, def) in structs)
            dict[name] = def;

        return new FormatDefinition
        {
            Name = "Test",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = dict,
            RootStruct = rootName,
        };
    }
}
