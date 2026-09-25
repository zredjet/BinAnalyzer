using BinAnalyzer.Core.Decoded;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Presentation.Tests;

public sealed class FieldKindMapperTests
{
    private static readonly DecodedStruct Parent = new() { Name = "p", StructType = "P", Offset = 0, Size = 0, Children = [] };

    [Fact]
    public void Root_IsRoot_OnlyWithoutParent()
    {
        FieldKindMapper.Map(Parent, null).Should().Be(FieldKind.Root);
        FieldKindMapper.Map(Parent, Parent).Should().Be(FieldKind.Struct);
    }

    [Fact]
    public void Padding_BeatsEverything()
    {
        var n = new DecodedInteger { Name = "crc", Offset = 0, Size = 4, Value = 0, ChecksumAlgorithm = "crc32", IsPadding = true };
        FieldKindMapper.Map(n, Parent).Should().Be(FieldKind.Pad);
    }

    [Fact]
    public void Error_And_Compressed()
    {
        FieldKindMapper.Map(new DecodedError { Name = "e", Offset = 0, Size = 0, ErrorMessage = "x" }, Parent).Should().Be(FieldKind.Error);
        FieldKindMapper.Map(new DecodedCompressed { Name = "d", Offset = 0, Size = 1, Algorithm = "zlib", CompressedSize = 1, DecompressedSize = 1 }, Parent).Should().Be(FieldKind.Zip);
    }

    [Fact]
    public void Checksum_BeatsMagic_AndLen()
    {
        var b = new DecodedBytes { Name = "signature", Offset = 0, Size = 4, RawBytes = new byte[4], ChecksumAlgorithm = "crc32", ValidationPassed = true };
        FieldKindMapper.Map(b, Parent).Should().Be(FieldKind.Crc);
        var i = new DecodedInteger { Name = "length", Offset = 0, Size = 4, Value = 0, ChecksumAlgorithm = "adler32" };
        FieldKindMapper.Map(i, Parent).Should().Be(FieldKind.Crc);
    }

    [Fact]
    public void Magic_FromExpectedBytes_OrName()
    {
        FieldKindMapper.Map(new DecodedBytes { Name = "x", Offset = 0, Size = 4, RawBytes = new byte[4], ValidationPassed = true }, Parent).Should().Be(FieldKind.Magic);
        FieldKindMapper.Map(new DecodedBytes { Name = "magic", Offset = 0, Size = 4, RawBytes = new byte[4] }, Parent).Should().Be(FieldKind.Magic);
        FieldKindMapper.Map(new DecodedString { Name = "signature", Offset = 0, Size = 4, Value = "RIFF", Encoding = "ASCII" }, Parent).Should().Be(FieldKind.Magic);
        FieldKindMapper.Map(new DecodedBytes { Name = "blob", Offset = 0, Size = 4, RawBytes = new byte[4] }, Parent).Should().Be(FieldKind.Bytes);
    }

    [Fact]
    public void Tag_FromFlags_OrShortNamedString()
    {
        FieldKindMapper.Map(new DecodedString { Name = "x", Offset = 0, Size = 4, Value = "IHDR", Encoding = "ASCII", Flags = [] }, Parent).Should().Be(FieldKind.Tag);
        FieldKindMapper.Map(new DecodedString { Name = "chunk_type", Offset = 0, Size = 4, Value = "IHDR", Encoding = "ASCII" }, Parent).Should().Be(FieldKind.Tag);
        FieldKindMapper.Map(new DecodedString { Name = "title", Offset = 0, Size = 40, Value = "hello", Encoding = "UTF-8" }, Parent).Should().Be(FieldKind.Str);
    }

    [Theory]
    [InlineData("length", FieldKind.Len)]
    [InlineData("data_size", FieldKind.Len)]
    [InlineData("num_entries", FieldKind.Len)]
    [InlineData("count", FieldKind.Len)]
    [InlineData("width", FieldKind.Num)]
    [InlineData("counter", FieldKind.Num)]
    public void Len_Heuristic_OnIntegerNames(string name, FieldKind expected)
    {
        FieldKindMapper.Map(new DecodedInteger { Name = name, Offset = 0, Size = 4, Value = 0 }, Parent).Should().Be(expected);
    }

    [Fact]
    public void TypeDefaults()
    {
        FieldKindMapper.Map(new DecodedFloat { Name = "f", Offset = 0, Size = 4, Value = 1, Precision = FloatPrecision.Single }, Parent).Should().Be(FieldKind.Num);
        FieldKindMapper.Map(new DecodedFlags { Name = "f", Offset = 0, Size = 1, RawValue = 0, FlagStates = [] }, Parent).Should().Be(FieldKind.Flags);
        FieldKindMapper.Map(new DecodedBitfield { Name = "b", Offset = 0, Size = 1, RawValue = 0, Fields = [] }, Parent).Should().Be(FieldKind.Bitfield);
        FieldKindMapper.Map(new DecodedVirtual { Name = "v", Offset = 0, Size = 0, Value = 1 }, Parent).Should().Be(FieldKind.Virtual);
        FieldKindMapper.Map(new DecodedArray { Name = "a", Offset = 0, Size = 0, Elements = [] }, Parent).Should().Be(FieldKind.Array);
    }

    [Fact]
    public void CssClass_IsLowercasePrefixed()
    {
        FieldKindMapper.CssClass(FieldKind.Num).Should().Be("k-num");
        FieldKindMapper.CssClass(FieldKind.Crc).Should().Be("k-crc");
    }
}
