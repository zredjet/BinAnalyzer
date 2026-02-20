using BinAnalyzer.Tui;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Tui.Tests;

public sealed class HexPaneTests
{
    [Fact]
    public void GenerateHexDump_BasicOutput_HasHeaderAndData()
    {
        var data = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        var result = HexPane.GenerateHexDump(data, 0, 4);

        result.Should().Contain("Offset");
        result.Should().Contain("00000000");
        result.Should().Contain("89 50 4E 47");
        result.Should().Contain(".PNG");
    }

    [Fact]
    public void GenerateHexDump_HighlightsSelectedBytes()
    {
        var data = new byte[32];
        data[4] = 0xAA;
        data[5] = 0xBB;

        var result = HexPane.GenerateHexDump(data, 4, 2);

        // Should contain marker line with ^^ under selected bytes
        result.Should().Contain("^^");
    }

    [Fact]
    public void GenerateHexDump_EmptyData_ReturnsHeaderOnly()
    {
        var data = Array.Empty<byte>();

        var result = HexPane.GenerateHexDump(data, 0, 0);

        result.Should().Contain("Offset");
        // No data lines (just header)
        var lines = result.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2); // header + separator
    }

    [Fact]
    public void GenerateHexDump_AsciiColumn_ShowsPrintableChars()
    {
        var data = "Hello, World!\0\x01\x02"u8.ToArray();

        var result = HexPane.GenerateHexDump(data, 0, data.Length);

        result.Should().Contain("Hello, World!...");
    }

    [Fact]
    public void GenerateHexDump_SelectionAtOffset_MarkerOnCorrectLine()
    {
        var data = new byte[48]; // 3 lines of 16 bytes
        data[20] = 0xFF; // Second line, offset 4

        var result = HexPane.GenerateHexDump(data, 20, 1);
        var lines = result.Split('\n');

        // Find the marker line (contains ^^)
        var markerLine = lines.FirstOrDefault(l => l.Contains("^^"));
        markerLine.Should().NotBeNull();

        // Marker should be positioned after offset+hex columns
        // Byte 20 is at position 4 in the second 16-byte line
        markerLine.Should().Contain("^^");
    }
}
