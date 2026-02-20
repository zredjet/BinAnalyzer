using BinAnalyzer.Web.Pages;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Web.Tests;

public sealed class FormatFileSizeTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1, "1 B")]
    [InlineData(512, "512 B")]
    [InlineData(1023, "1023 B")]
    public void Bytes_Range(long bytes, string expected)
    {
        Home.FormatFileSize(bytes).Should().Be(expected);
    }

    [Theory]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(10240, "10.0 KB")]
    [InlineData(1048575, "1024.0 KB")]
    public void Kilobytes_Range(long bytes, string expected)
    {
        Home.FormatFileSize(bytes).Should().Be(expected);
    }

    [Theory]
    [InlineData(1048576, "1.0 MB")]
    [InlineData(10485760, "10.0 MB")]
    [InlineData(104857600, "100.0 MB")]
    public void Megabytes_Range(long bytes, string expected)
    {
        Home.FormatFileSize(bytes).Should().Be(expected);
    }

    [Theory]
    [InlineData(1073741824, "1.0 GB")]
    [InlineData(2147483648, "2.0 GB")]
    public void Gigabytes_Range(long bytes, string expected)
    {
        Home.FormatFileSize(bytes).Should().Be(expected);
    }
}
