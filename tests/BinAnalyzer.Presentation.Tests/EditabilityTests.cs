using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Patching;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Presentation.Tests;

/// <summary>REQ-169: 編集可否と依存チェックサムの導出。</summary>
public class EditabilityTests
{
    private static DecodedStruct Tree()
    {
        var inner = new DecodedStruct
        {
            Name = "body", StructType = "body", Offset = 8, Size = 8,
            Children =
            [
                new DecodedInteger { Name = "length", Offset = 8, Size = 4, Value = 4, DslType = FieldType.UInt32, Endianness = Endianness.Big },
                new DecodedInteger { Name = "kind", Offset = 12, Size = 1, Value = 2, DslType = FieldType.UInt8, EnumRef = "kinds" },
                new DecodedInteger { Name = "bits", Offset = 13, Size = 3, BitOffset = 0, Value = 1, DslType = FieldType.UInt8 },
                new DecodedInteger { Name = "varint", Offset = 14, Size = 2, Value = 300, DslType = FieldType.ULeb128 },
            ],
        };
        var zip = new DecodedCompressed
        {
            Name = "zip", Offset = 16, Size = 4, CompressedSize = 4, DecompressedSize = 2, Algorithm = "zlib",
            DecodedContent = new DecodedStruct
            {
                Name = "zip", StructType = "z", Offset = 0, Size = 2,
                Children = [new DecodedInteger { Name = "inner", Offset = 0, Size = 2, Value = 1, DslType = FieldType.UInt16, Endianness = Endianness.Big }],
            },
        };
        return new DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 28,
            Children =
            [
                new DecodedBytes { Name = "magic", Offset = 0, Size = 4, RawBytes = new byte[4], DslType = FieldType.Bytes },
                new DecodedInteger { Name = "crc_head", Offset = 4, Size = 4, Value = 0, DslType = FieldType.UInt32, Endianness = Endianness.Big, ChecksumAlgorithm = "crc32", ChecksumValid = true, ChecksumCoverage = [new ByteRange(0, 4)] },
                inner,
                zip,
                new DecodedBytes { Name = "digest", Offset = 20, Size = 8, RawBytes = new byte[8], DslType = FieldType.Bytes, ChecksumAlgorithm = "md5", ChecksumValid = false, ChecksumCoverage = [new ByteRange(0, 4), new ByteRange(8, 12)] },
            ],
        };
    }

    [Fact]
    public void Of_ClassifiesNodes_AndExplainsWhyNot()
    {
        var index = NodeIndex.Build(Tree());
        int Id(string p) => index.IdOf(index.ByPath(p)!);

        FieldEditability.Of(index, 0).Should().Match<EditabilityInfo>(e => !e.CanEdit && e.Reason!.Contains("ルート"));
        FieldEditability.Of(index, Id("magic")).Kind.Should().Be(EditKind.Bytes);
        FieldEditability.Of(index, Id("crc_head")).Kind.Should().Be(EditKind.Integer);
        FieldEditability.Of(index, Id("body.kind")).Kind.Should().Be(EditKind.Enum);
        FieldEditability.Of(index, Id("body")).Should().Match<EditabilityInfo>(e => !e.CanEdit && e.Reason!.Contains("構造体"));
        FieldEditability.Of(index, Id("body.bits")).Reason.Should().Contain("ビットストリーム");
        FieldEditability.Of(index, Id("body.varint")).Reason.Should().Contain("可変長");
        FieldEditability.Of(index, Id("zip")).Reason.Should().Contain("再圧縮");
        FieldEditability.Of(index, Id("zip.inner")).Reason.Should().Contain("圧縮ストリーム内");
    }

    [Fact]
    public void Of_FlagsLengthFields_AsLayoutShifting()
    {
        var index = NodeIndex.Build(Tree());
        FieldEditability.Of(index, index.IdOf(index.ByPath("body.length")!)).MayShiftLayout.Should().BeTrue();
        FieldEditability.Of(index, index.IdOf(index.ByPath("body.kind")!)).MayShiftLayout.Should().BeFalse();
    }

    [Fact]
    public void Covering_ReturnsChecksumsWhoseCoverageOverlaps()
    {
        var index = NodeIndex.Build(Tree());

        var magic = ChecksumDependencies.Covering(index, new ByteRange(0, 4));
        magic.Select(d => d.Path).Should().Equal("crc_head", "digest");
        magic[0].Algorithm.Should().Be("crc32");
        magic[0].Valid.Should().BeTrue();
        magic[1].Valid.Should().BeFalse();

        ChecksumDependencies.Covering(index, new ByteRange(12, 1)).Select(d => d.Path).Should().Equal("digest");
        ChecksumDependencies.Covering(index, new ByteRange(4, 4)).Should().BeEmpty("crc 自身の範囲は誰の算出対象でもない");
        ChecksumDependencies.Covering(index, new ByteRange(0, 0)).Should().BeEmpty();
    }

    [Fact]
    public void Covering_SkipsUnverifiedChecksums()
    {
        // REQ-187: 計算できないアルゴリズムのチェックサムは再計算されないので予告しない
        var root = new DecodedStruct
        {
            Name = "r", StructType = "r", Offset = 0, Size = 8,
            Children =
            [
                new DecodedBytes { Name = "body", Offset = 0, Size = 4, RawBytes = new byte[4] },
                new DecodedInteger
                {
                    Name = "crc", Offset = 4, Size = 4, Value = 1, ChecksumAlgorithm = "crc32c", ChecksumValid = null,
                    ChecksumCoverage = [new ByteRange(0, 4)],
                },
            ],
        };

        ChecksumDependencies.Covering(NodeIndex.Build(root), new ByteRange(0, 4)).Should().BeEmpty();
    }

    [Fact]
    public void Covering_ExcludesRequestedNode()
    {
        var index = NodeIndex.Build(Tree());
        var digest = index.IdOf(index.ByPath("digest")!);
        ChecksumDependencies.Covering(index, new ByteRange(0, 4), excludeId: digest).Select(d => d.Path).Should().Equal("crc_head");
    }
}
