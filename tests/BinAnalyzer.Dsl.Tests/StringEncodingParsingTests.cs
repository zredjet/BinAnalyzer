using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

public class StringEncodingParsingTests
{
    [Theory]
    [InlineData("utf16le", FieldType.Utf16Le)]
    [InlineData("utf16-le", FieldType.Utf16Le)]
    [InlineData("utf16be", FieldType.Utf16Be)]
    [InlineData("utf16-be", FieldType.Utf16Be)]
    [InlineData("sjis", FieldType.ShiftJis)]
    [InlineData("shift_jis", FieldType.ShiftJis)]
    [InlineData("shift-jis", FieldType.ShiftJis)]
    [InlineData("latin1", FieldType.Latin1)]
    [InlineData("iso-8859-1", FieldType.Latin1)]
    public void Load_StringEncodingType_ParsesCorrectly(string typeName, FieldType expected)
    {
        var yaml = $"""
            name: test
            endianness: big
            root: root
            structs:
              root:
                - name: text
                  type: {typeName}
                  size: "10"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["root"].Fields[0];
        field.Type.Should().Be(expected);
        field.Size.Should().Be(10);
    }
}
