using BinAnalyzer.Gui.Desktop;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Gui.Tests;

public class GuiAutoCloseTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("no")]
    [InlineData("-5")]
    [InlineData("abc")]
    public void Disabled_ReturnsNull(string? value)
    {
        GuiAutoClose.Parse(value).Should().BeNull();
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("yes")]
    public void Enabled_UsesDefaultDelay(string value)
    {
        GuiAutoClose.Parse(value).Should().Be(GuiAutoClose.Default);
    }

    [Theory]
    [InlineData("500", 500)]
    [InlineData(" 3000 ", 3000)]
    [InlineData("60000", 60000)]
    public void Integer_IsMilliseconds(string value, int expectedMs)
    {
        GuiAutoClose.Parse(value).Should().Be(TimeSpan.FromMilliseconds(expectedMs));
    }
}
