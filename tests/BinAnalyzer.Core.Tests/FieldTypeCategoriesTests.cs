using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests;

public class FieldTypeCategoriesTests
{
    [Fact]
    public void IsCompressed_ExactlyTheSevenCompressionTypes()
    {
        Enum.GetValues<FieldType>().Where(FieldTypeCategories.IsCompressed).Should().BeEquivalentTo(
        [
            FieldType.Zlib, FieldType.Deflate, FieldType.Gzip,
            FieldType.Bzip2, FieldType.Lzma, FieldType.Zstd, FieldType.Lz4,
        ]);
    }

    [Theory]
    [InlineData(FieldType.Bytes)]
    [InlineData(FieldType.Struct)]
    [InlineData(FieldType.UInt32)]
    public void IsCompressed_FalseForNonCompressionTypes(FieldType type)
    {
        FieldTypeCategories.IsCompressed(type).Should().BeFalse();
    }
}
