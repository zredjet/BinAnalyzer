using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Patching;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>REQ-169: パッチ適用と依存チェックサムの再計算、デコーダが残す算出範囲。</summary>
public class BinaryPatcherTests
{
    private static FormatDefinition Format(params FieldDefinition[] fields) => new()
    {
        Name = "test",
        Endianness = Endianness.Big,
        Enums = new Dictionary<string, EnumDefinition>(),
        Flags = new Dictionary<string, FlagsDefinition>(),
        Structs = new Dictionary<string, StructDefinition> { ["root"] = new() { Name = "root", Fields = fields } },
        RootStruct = "root",
    };

    /// <summary>type(ascii 4) + data(u32) + crc(crc32 over [type, data])</summary>
    private static FormatDefinition ChunkFormat() => Format(
        new FieldDefinition { Name = "type", Type = FieldType.Ascii, Size = 4 },
        new FieldDefinition { Name = "data", Type = FieldType.UInt32 },
        new FieldDefinition { Name = "crc", Type = FieldType.UInt32, Checksum = new ChecksumSpec { Algorithm = "crc32", FieldNames = ["type", "data"] } });

    private static byte[] Chunk(string type, uint data)
    {
        var bytes = new byte[12];
        Encoding.ASCII.GetBytes(type).CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), data);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), Crc32Calculator.Compute(bytes.AsSpan(0, 8).ToArray()));
        return bytes;
    }

    private static Expression Expr(string text) => ExpressionParser.Parse(text);

    [Fact]
    public void Decoder_RecordsChecksumCoverage_ForFieldsMode()
    {
        var root = new BinaryDecoder().Decode(Chunk("IHDR", 1), ChunkFormat());
        var crc = (DecodedInteger)root.Children[2];
        crc.ChecksumValid.Should().BeTrue();
        crc.ChecksumCoverage.Should().Equal(new ByteRange(0, 4), new ByteRange(4, 4));
        crc.DslType.Should().Be(FieldType.UInt32);
        crc.Endianness.Should().Be(Endianness.Big);

        var data = (DecodedInteger)root.Children[1];
        data.ChecksumCoverage.Should().BeNull();
        data.DslType.Should().Be(FieldType.UInt32);
        ((DecodedString)root.Children[0]).DslType.Should().Be(FieldType.Ascii);
    }

    [Fact]
    public void Decoder_RecordsChecksumCoverage_ForRangeMode()
    {
        var format = Format(
            new FieldDefinition { Name = "a", Type = FieldType.UInt16 },
            new FieldDefinition { Name = "b", Type = FieldType.UInt16 },
            new FieldDefinition { Name = "sum", Type = FieldType.UInt16, Checksum = new ChecksumSpec { Algorithm = "fletcher16", FieldNames = [], Range = new ChecksumRange { OffsetExpression = Expr("0"), SizeExpression = Expr("4") } } });
        var root = new BinaryDecoder().Decode(new byte[6], format);
        ((DecodedInteger)root.Children[2]).ChecksumCoverage.Should().Equal(new ByteRange(0, 4));
    }

    [Fact]
    public void Apply_UpdatesBytes_AndRecalculatesCoveringChecksum()
    {
        var original = Chunk("IHDR", 1);
        var patch = new BytePatch(4, [0, 0, 0, 2]);

        var outcome = BinaryPatcher.Apply(original, ChunkFormat(), [patch]);

        outcome.Converged.Should().BeTrue();
        outcome.Data.Should().Equal(Chunk("IHDR", 2));
        outcome.Data.Should().NotBeSameAs(original);
        original.Should().Equal(Chunk("IHDR", 1), "入力は書き換えない");
        outcome.ChecksumFixes.Should().ContainSingle().Which.Should().Match<ChecksumFix>(f =>
            f.Path == "crc" && f.Range == new ByteRange(8, 4) && f.Algorithm == "crc32");

        var root = new BinaryDecoder().Decode(outcome.Data, ChunkFormat());
        ((DecodedInteger)root.Children[1]).Value.Should().Be(2);
        ((DecodedInteger)root.Children[2]).ChecksumValid.Should().BeTrue();
    }

    [Fact]
    public void Apply_WithoutRecalculation_LeavesChecksumStale()
    {
        var outcome = BinaryPatcher.Apply(Chunk("IHDR", 1), ChunkFormat(), [new BytePatch(4, [0, 0, 0, 2])], recalculateChecksums: false);
        outcome.ChecksumFixes.Should().BeEmpty();
        ((DecodedInteger)new BinaryDecoder().Decode(outcome.Data, ChunkFormat()).Children[2]).ChecksumValid.Should().BeFalse();
    }

    [Fact]
    public void Apply_ExplicitPatchOnChecksumField_IsNotOverwritten()
    {
        var outcome = BinaryPatcher.Apply(Chunk("IHDR", 1), ChunkFormat(), [new BytePatch(8, [1, 2, 3, 4])]);
        outcome.ChecksumFixes.Should().BeEmpty();
        outcome.Data.AsSpan(8, 4).ToArray().Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void Apply_LeavesUnrelatedInvalidChecksumAlone()
    {
        // 2 チャンク: 2 つ目の crc は最初から壊れている
        var format = Format(
            new FieldDefinition { Name = "chunks", Type = FieldType.Struct, StructRef = "chunk", Repeat = new RepeatMode.Count(Expr("2")) });
        format = new FormatDefinition
        {
            Name = format.Name, Endianness = format.Endianness, Enums = format.Enums, Flags = format.Flags, RootStruct = "root",
            Structs = new Dictionary<string, StructDefinition>
            {
                ["root"] = format.Structs["root"],
                ["chunk"] = new() { Name = "chunk", Fields = ChunkFormat().Structs["root"].Fields },
            },
        };
        var data = Chunk("AAAA", 1).Concat(Chunk("BBBB", 5)).ToArray();
        data[^1] ^= 0xFF;

        var outcome = BinaryPatcher.Apply(data, format, [new BytePatch(4, [0, 0, 0, 9])]);

        outcome.ChecksumFixes.Select(f => f.Path).Should().Equal("chunks[0].crc");
        outcome.Data.AsSpan(12).ToArray().Should().Equal(data.AsSpan(12).ToArray(), "無関係なチャンクは触らない");
    }

    [Fact]
    public void Apply_NestedChecksums_IterateUntilStable()
    {
        // a(u16) b(u16) inner(crc16 over [a,b]) outer(crc32 over range [0,6) = a,b,inner)
        var format = Format(
            new FieldDefinition { Name = "a", Type = FieldType.UInt16 },
            new FieldDefinition { Name = "b", Type = FieldType.UInt16 },
            new FieldDefinition { Name = "inner", Type = FieldType.UInt16, Checksum = new ChecksumSpec { Algorithm = "crc16-ccitt", FieldNames = ["a", "b"] } },
            new FieldDefinition { Name = "outer", Type = FieldType.UInt32, Checksum = new ChecksumSpec { Algorithm = "crc32", FieldNames = [], Range = new ChecksumRange { OffsetExpression = Expr("0"), SizeExpression = Expr("6") } } });
        var data = new byte[10];
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(0), 1);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(2), 2);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(4), Crc16Calculator.ComputeCcitt(data.AsSpan(0, 4).ToArray()));
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(6), Crc32Calculator.Compute(data.AsSpan(0, 6).ToArray()));
        new BinaryDecoder().Decode(data, format).Children.OfType<DecodedInteger>().Where(i => i.ChecksumValid.HasValue).Should().OnlyContain(i => i.ChecksumValid == true);

        var outcome = BinaryPatcher.Apply(data, format, [new BytePatch(0, [0, 7])]);

        outcome.Converged.Should().BeTrue();
        outcome.ChecksumFixes.Select(f => f.Path).Should().Contain("inner").And.Contain("outer");
        var root = new BinaryDecoder().Decode(outcome.Data, format);
        root.Children.OfType<DecodedInteger>().Where(i => i.ChecksumValid.HasValue).Should().OnlyContain(i => i.ChecksumValid == true);
        ((DecodedInteger)root.Children[0]).Value.Should().Be(7);
    }

    [Fact]
    public void Apply_HashChecksum_BytesFieldIsRewritten()
    {
        var format = Format(
            new FieldDefinition { Name = "payload", Type = FieldType.Bytes, Size = 4 },
            new FieldDefinition { Name = "digest", Type = FieldType.Bytes, Size = 16, Checksum = new ChecksumSpec { Algorithm = "md5", FieldNames = ["payload"] } });
        var data = new byte[20];
        MD5.HashData(data.AsSpan(0, 4)).CopyTo(data, 4);

        var outcome = BinaryPatcher.Apply(data, format, [new BytePatch(0, [1, 2, 3, 4])]);

        outcome.ChecksumFixes.Should().ContainSingle().Which.Path.Should().Be("digest");
        outcome.Data.AsSpan(4, 16).ToArray().Should().Equal(MD5.HashData(new byte[] { 1, 2, 3, 4 }));
        ((DecodedBytes)new BinaryDecoder().Decode(outcome.Data, format).Children[1]).ChecksumValid.Should().BeTrue();
    }

    [Fact]
    public void Apply_LittleEndianChecksumField_UsesFieldEndianness()
    {
        var format = Format(
            new FieldDefinition { Name = "data", Type = FieldType.UInt32 },
            new FieldDefinition { Name = "crc", Type = FieldType.UInt32, Endianness = Endianness.Little, Checksum = new ChecksumSpec { Algorithm = "crc32", FieldNames = ["data"] } });
        var data = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), Crc32Calculator.Compute(new byte[4]));

        var outcome = BinaryPatcher.Apply(data, format, [new BytePatch(0, [0, 0, 0, 1])]);

        var expected = Crc32Calculator.Compute(new byte[] { 0, 0, 0, 1 });
        BinaryPrimitives.ReadUInt32LittleEndian(outcome.Data.AsSpan(4)).Should().Be(expected);
    }

    [Fact]
    public void Apply_PatchOutOfBounds_Throws()
    {
        var act = () => BinaryPatcher.Apply(new byte[4], ChunkFormat(), [new BytePatch(2, [1, 2, 3])]);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Apply_NoPatches_ReturnsCopy()
    {
        var data = Chunk("IHDR", 1);
        var outcome = BinaryPatcher.Apply(data, ChunkFormat(), []);
        outcome.Data.Should().Equal(data).And.NotBeSameAs(data);
        outcome.ChecksumFixes.Should().BeEmpty();
    }
}
