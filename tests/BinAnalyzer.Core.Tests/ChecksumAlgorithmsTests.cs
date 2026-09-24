using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests;

/// <summary>REQ-187: アルゴリズムの名前・種類・表示名は 1 つの表から。</summary>
public class ChecksumAlgorithmsTests
{
    [Fact]
    public void Table_NamesAreUnique_AndSetsAreDerivedFromIt()
    {
        ChecksumAlgorithms.All.Select(a => a.Name).Should().OnlyHaveUniqueItems();
        ChecksumAlgorithms.IntegerAlgorithms.Should().BeEquivalentTo(
            ChecksumAlgorithms.All.Where(a => a.Kind == ChecksumKind.Integer).Select(a => a.Name));
        ChecksumAlgorithms.HashAlgorithms.Should().BeEquivalentTo(
            ChecksumAlgorithms.All.Where(a => a.Kind == ChecksumKind.Hash).Select(a => a.Name));
        ChecksumAlgorithms.IntegerAlgorithms.Should().NotIntersectWith(ChecksumAlgorithms.HashAlgorithms);
    }

    [Theory]
    [InlineData("crc32", "CRC-32")]
    [InlineData("CRC16-CCITT", "CRC-16/CCITT")]
    [InlineData("sha512", "SHA-512")]
    [InlineData("crc32c", "crc32c")]
    public void DisplayName_FromTable_OrTheNameItself(string name, string expected)
    {
        ChecksumAlgorithms.DisplayName(name).Should().Be(expected);
    }

    [Fact]
    public void IsKnown_IgnoresCase()
    {
        ChecksumAlgorithms.IsKnown("Adler32").Should().BeTrue();
        ChecksumAlgorithms.IsKnown("crc32c").Should().BeFalse();
        ChecksumAlgorithms.IsHashAlgorithm("SHA1").Should().BeTrue();
        ChecksumAlgorithms.IsIntegerAlgorithm("sha1").Should().BeFalse();
    }
}
